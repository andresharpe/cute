using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scriban.Runtime;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Cute.Lib.Scriban;

/// <summary>
/// Uberall-backed Google review snapshot for the 'DataLocation.AllLocations.GoogleReviewSnapshot'
/// generate job, which fills the dataLocation 'googleBusinessReviewSnapshot' field once a month.
///
/// Uberall supplies every fact (ratings, text, author, dates, links). Cute computes the score,
/// cleans the text and applies the deterministic pre-filters. The model only decides which of the
/// remaining 4- and 5-star reviews may be quoted, and never sees the rating or the author.
/// <see cref="BuildGoogleReviewSnapshotField"/> then builds the stored JSON from Uberall's data and
/// runs the QA checks; a location that fails any check, or whose fetch was incomplete, is not
/// written and keeps its current snapshot.
///
/// Requires 'Cute__UberallApiKey'. Take-downs are the review ids in the dataLocation field
/// 'googleBusinessReviewExclusions', which the query must return.
/// </summary>
public partial class CuteFunctions
{
    internal const string ReviewSource = "Google reviews via Uberall";
    internal const string ReviewSelectionRule = "Selected 4- and 5-star Google reviews from members and guests.";
    internal const double ReviewMinAverage = 4.0;
    internal const int ReviewMinCount = 3;
    internal const int ReviewCandidateCount = 40;
    internal const int ReviewMaxTestimonials = 5;
    internal const int ReviewMinWords = 8;
    internal const int ReviewMaxChars = 1200;
    internal const int ReviewSoftMaxChars = 900;
    internal const int ReviewMaxTransitItems = 10;
    internal const string ReviewExclusionsField = "googleBusinessReviewExclusions";

    internal static readonly string[] ReviewAllowedReasons =
    [
        "contract-or-billing",
        "contradicts-facts",
        "not-about-our-service",
        "mixed-or-negative",
        "abusive-or-off-topic",
        "too-short",
    ];

    [ScriptMemberIgnore]
    public static string? UberallApiKey { get; set; }

    [ScriptMemberIgnore]
    public static string UberallBaseUrl { get; set; } = "https://uberall.com";

    [ScriptMemberIgnore]
    public static HttpClient UberallHttpClient { get; set; } = new() { Timeout = TimeSpan.FromSeconds(60) };

    /// <summary>Waits between Uberall retries. Replaced in tests.</summary>
    [ScriptMemberIgnore]
    public static Func<TimeSpan, Task> UberallDelay { get; set; } = d => Task.Delay(d);

    private static readonly object _reviewRunLock = new();

    private static IReadOnlyDictionary<string, UberallLocationMatch>? _uberallLocations;

    private static readonly ConcurrentDictionary<string, Lazy<ReviewLocation>> _reviewLocations = [];

    // Review id -> location number, for testimonials accepted so far in this run.
    private static readonly ConcurrentDictionary<string, string> _testimonialIdsInRun = [];

    private static readonly JsonSerializerSettings _uberallJsonSettings = new() { DateParseHandling = DateParseHandling.None };

    /// <summary>
    /// Forgets everything fetched in an earlier run. The scheduler runs jobs in one long-lived
    /// process, so the monthly job must not reuse last month's Uberall data.
    /// </summary>
    [ScriptMemberIgnore]
    public static void StartGoogleReviewRun()
    {
        lock (_reviewRunLock)
        {
            _uberallLocations = null;
            _reviewLocations.Clear();
            _testimonialIdsInRun.Clear();
        }
    }

    /// <summary>
    /// The reviews the model must check for one centre, one JSON object per line ({id, date, text}).
    /// Never includes the rating or the author. Scriban: {{ cute.google_reviews dataLocation }}
    /// </summary>
    public static string GoogleReviews(object location)
    {
        var entry = ToReviewEntry(location);

        var review = GetReviewLocation(entry);

        if (review.Status != ReviewLocationStatus.Ready)
        {
            return $"(not available: {review.Detail})";
        }

        if (review.Candidates.Count == 0)
        {
            return "(no reviews to check)";
        }

        return string.Join('\n', review.Candidates.Select(c => new JObject
        {
            ["id"] = c.Id,
            ["date"] = c.PublishedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["text"] = c.Text,
        }.ToString(Formatting.None)));
    }

    /// <summary>
    /// The building facts the model checks reviews against, in the reference implementation's shape
    /// (factsBlock in iwg-tesseract scripts/_reviews.mjs): name, address, 24/7 access, amenities and up
    /// to 10 transit items (from nearbyTransit, or transitInformation where that is all there is).
    /// Nothing personal. Scriban: {{ cute.review_building_facts dataLocation }}
    /// </summary>
    public static string ReviewBuildingFacts(object location)
    {
        var entry = ToReviewEntry(location);

        string? S(string path) => entry.SelectToken(path)?.Type is JTokenType.String or JTokenType.Integer
            ? entry.SelectToken(path)!.ToString().Trim()
            : null;

        var lines = new List<string>
        {
            $"name: {S("name")}",
            $"address: {string.Join(", ", new[] { S("address1"), S("address2"), S("zipPostCode"), S("city") }.Where(v => !string.IsNullOrEmpty(v)))}",
        };

        if (entry.SelectToken("isTwentyFourSevenAccessible") is { Type: JTokenType.Boolean } allHours)
        {
            lines.Add($"24/7 access: {(allHours.Value<bool>() ? "yes" : "no")}");
        }

        var amenities = (entry.SelectToken("dataLocationAmenityEntriesCollection.items") as JArray ?? entry.SelectToken("dataLocationAmenityEntries") as JArray ?? [])
            .Select(a => (a is JObject o ? o["name"] ?? o["title"] : a)?.ToString().Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (amenities.Count > 0) lines.Add($"amenities: {string.Join("; ", amenities)}");

        // nearbyTransit replaces transitInformation on master; read whichever the query returns.
        var transit = ReviewTransitItems(entry.SelectToken("nearbyTransit") is { Type: not JTokenType.Null } nearby
            ? nearby
            : entry.SelectToken("transitInformation"));

        if (transit.Count > 0) lines.Add($"nearby transport: {string.Join("; ", transit)}");

        return $"BUILDING FACTS (from the CMS)\n{string.Join('\n', lines)}";
    }

    internal static List<string> ReviewTransitItems(JToken? transit)
    {
        IEnumerable<string?> items = transit switch
        {
            null => [],
            JValue { Type: JTokenType.String } v => v.Value<string>()!.Split('\n'),
            JArray a => a.Select(DescribeTransitItem),
            JObject o => (o.Properties().Select(p => p.Value).OfType<JArray>().FirstOrDefault() ?? []).Select(DescribeTransitItem),
            _ => [],
        };

        return items
            .Select(i => i?.Trim().TrimStart('-', '*', '•').Trim())
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Take(ReviewMaxTransitItems)
            .ToList()!;

        static string? DescribeTransitItem(JToken item)
        {
            if (item is not JObject o) return item.ToString();

            var name = (o["name"] ?? o["title"])?.ToString();
            var type = o["type"]?.ToString()?.Replace('_', ' ');

            return string.IsNullOrWhiteSpace(type) ? name : $"{name} ({type})";
        }
    }

    // ---- Run-scoped data ----------------------------------------------------------------------------

    /// <summary>
    /// Null when the entry is in scope and its Uberall data is complete; otherwise why it is left
    /// untouched (closed, hidden, not in Uberall, incomplete fetch...).
    /// </summary>
    [ScriptMemberIgnore]
    public static string? GetGoogleReviewSkipReason(JObject entry, DateTime nowUtc)
    {
        if (entry.SelectToken("isVisibleOnWebsites") is { Type: JTokenType.Boolean } visible && !visible.Value<bool>())
        {
            return "not visible on websites";
        }

        var closeDate = entry.SelectToken("closeDate");
        if (closeDate is not null && closeDate.Type != JTokenType.Null && ParseReviewDate(closeDate.ToString()) is { } closed && closed.Date <= nowUtc.Date)
        {
            return $"closed on {closed:yyyy-MM-dd}";
        }

        var review = GetReviewLocation(entry);

        return review.Status == ReviewLocationStatus.Ready ? null : review.Detail;
    }

    [ScriptMemberIgnore]
    internal static ReviewLocation GetReviewLocation(JObject entry)
    {
        var locationNumber = ReviewLocationNumber(entry);
        var excludedIds = ReviewExclusionIds(entry);

        return _reviewLocations.GetOrAdd(locationNumber,
            n => new Lazy<ReviewLocation>(() => LoadReviewLocation(n, excludedIds, DateTime.UtcNow))).Value;
    }

    private static ReviewLocation LoadReviewLocation(string locationNumber, IReadOnlySet<string> excludedIds, DateTime nowUtc)
    {
        var matches = GetUberallLocations();

        if (!matches.TryGetValue(locationNumber, out var match))
        {
            return ReviewLocation.NotReady(locationNumber, ReviewLocationStatus.Unmatched, "not in Uberall");
        }

        if (match.UberallIds.Count > 1)
        {
            return ReviewLocation.NotReady(locationNumber, ReviewLocationStatus.Ambiguous,
                $"Uberall identifier on {match.UberallIds.Count} locations ({string.Join(", ", match.UberallIds)})");
        }

        var uberallId = match.UberallIds[0];

        UberallReviewFetch fetch;
        try
        {
            fetch = FetchUberallReviews(uberallId).GetAwaiter().GetResult();
        }
        catch (UberallApiException ex) when (!ex.IsAuthFailure)
        {
            return ReviewLocation.NotReady(locationNumber, ReviewLocationStatus.FetchFailed, $"Uberall fetch failed: {ex.Message}", uberallId);
        }

        if (!fetch.Complete)
        {
            return ReviewLocation.NotReady(locationNumber, ReviewLocationStatus.Incomplete,
                $"incomplete fetch ({fetch.Reviews.Count} of {fetch.ReportedCount} reviews)", uberallId);
        }

        return BuildReviewLocation(locationNumber, uberallId, fetch.Reviews, excludedIds, nowUtc);
    }

    /// <summary>Scores the location and picks the candidates for the model. Pure: no I/O.</summary>
    internal static ReviewLocation BuildReviewLocation(string locationNumber, long uberallId,
        IReadOnlyList<UberallReview> reviews, IReadOnlySet<string> excludedIds, DateTime fetchedAtUtc)
    {
        var score = ComputeReviewScore(reviews);

        var placeId = reviews
            .Select(r => ExtractGooglePlaceId(r.DirectLink))
            .FirstOrDefault(p => p is not null);

        var candidates = new List<UberallReview>();
        var rejected = new List<(UberallReview Review, string Reason)>();

        // Good quotes next to a hidden low score would mislead, so there are none to check.
        if (score.ShowScore)
        {
            var recentGood = reviews
                .Where(r => r.Rating is 4 or 5)
                .OrderByDescending(r => r.PublishedAt ?? DateTime.MinValue)
                .ThenByDescending(r => r.Id, StringComparer.Ordinal)
                .Take(ReviewCandidateCount);

            foreach (var review in recentGood)
            {
                if (excludedIds.Contains(review.Id))
                {
                    rejected.Add((review, "take-down"));
                    continue;
                }

                var reason = ReviewPrefilterReason(review.Text);

                if (reason is null)
                {
                    candidates.Add(review);
                }
                else
                {
                    rejected.Add((review, reason));
                }
            }
        }

        return new ReviewLocation(locationNumber, ReviewLocationStatus.Ready, null, uberallId, reviews, score,
            placeId, fetchedAtUtc, candidates, rejected);
    }

    internal static ReviewScore ComputeReviewScore(IReadOnlyCollection<UberallReview> reviews)
    {
        var count = reviews.Count;

        if (count == 0) return new ReviewScore(0, null, false);

        var mean = reviews.Average(r => (double)r.Rating);

        return new ReviewScore(count, Math.Round(mean, 2, MidpointRounding.AwayFromZero), mean >= ReviewMinAverage && count >= ReviewMinCount);
    }

    // ---- Text rules ---------------------------------------------------------------------------------

    /// <summary>
    /// Cleans review text without changing any words: removes zero-width spaces, the BOM and bidi
    /// embedding/isolate controls, normalises line ends, turns tabs into spaces, strips spaces before
    /// line ends, collapses 3+ newlines to 2 and trims. Keeps ZWJ/ZWNJ and the LRM/RLM marks.
    /// </summary>
    internal static string CleanReviewText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (IsRemovedFormatChar(c)) continue;
            sb.Append(c);
        }

        var cleaned = sb.ToString()
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace('\t', ' ');

        cleaned = Regex.Replace(cleaned, " +\n", "\n");
        cleaned = Regex.Replace(cleaned, "\n{3,}", "\n\n");

        return cleaned.Trim();
    }

    private static bool IsRemovedFormatChar(char c) =>
        c is '​' or '﻿' or (>= '‪' and <= '‮') or (>= '⁦' and <= '⁩');

    /// <summary>
    /// Words in a review, counted as the reference implementation does: when the text contains Han,
    /// Hiragana, Katakana or Thai, every 2 non-space characters count as a word; otherwise a word is a
    /// space-separated run with a letter or digit in it.
    /// </summary>
    internal static int CountReviewWords(string text)
    {
        var t = text.Trim();

        if (t.Length == 0) return 0;

        if (t.EnumerateRunes().Any(r => IsNoSpaceScript(r.Value)))
        {
            return t.EnumerateRunes().Count(r => !Rune.IsWhiteSpace(r)) / 2;
        }

        return t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Count(w => w.Any(char.IsLetterOrDigit));
    }

    private static bool IsNoSpaceScript(int cp) =>
        cp is (>= 0x4E00 and <= 0x9FFF) or (>= 0x3400 and <= 0x4DBF) or (>= 0x20000 and <= 0x2A6DF) or (>= 0xF900 and <= 0xFAFF) // Han
            or (>= 0x3040 and <= 0x309F) // Hiragana
            or (>= 0x30A0 and <= 0x30FF) or (>= 0x31F0 and <= 0x31FF) or (>= 0xFF66 and <= 0xFF9F) // Katakana
            or (>= 0x0E00 and <= 0x0E7F); // Thai

    // Contact-detail patterns as in the reference implementation (iwg-tesseract scripts/_reviews.mjs).
    private static readonly Regex _reviewEmail = new(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.Compiled);
    private static readonly Regex _reviewUrl = new(@"\bhttps?://|\bwww\.[a-z0-9-]+\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _reviewDate = new(@"\b\d{1,4}[./-]\d{1,2}[./-]\d{1,4}\b", RegexOptions.Compiled);
    // An international (+49 ...) or trunk-prefixed (0 ...) number, or three digit groups, of 8+ digits.
    private static readonly Regex _reviewPhoneRun = new(@"(?:\+|\b0)\d[\d\s()./-]{6,}\d|\b\d{3,}[\s./-]\d{3,}[\s./-]?\d{2,}\b", RegexOptions.Compiled);
    private static readonly Regex _reviewHtml = new(@"<\s*/?\s*[a-zA-Z][^>]*>|&(#\d+|#x[0-9a-fA-F]+|[a-zA-Z]+);", RegexOptions.Compiled);
    private static readonly Regex _reviewMachineTranslation = new(@"\((Translated by Google|Original)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _reviewLanguageCode = new(@"^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.Compiled);
    private static readonly Regex _googlePlaceId = new(@"^[A-Za-z0-9_-]{10,300}$", RegexOptions.Compiled);

    internal static bool HasReviewContactDetails(string text)
    {
        if (_reviewEmail.IsMatch(text) || _reviewUrl.IsMatch(text)) return true;

        var withoutDates = _reviewDate.Replace(text, " ");

        return _reviewPhoneRun.Matches(withoutDates).Any(m => m.Value.Count(char.IsAsciiDigit) >= 8);
    }

    internal static bool HasReviewControlChars(string text) =>
        text.Any(c => (char.IsControl(c) && c != '\n') || IsRemovedFormatChar(c));

    /// <summary>
    /// The deterministic pre-filter. Null when the review may go to the model. Besides the spec's
    /// too-long, too-short and contact-details, it also drops text the QA gate would reject later
    /// (Google machine translation, markup, control characters), so one such review can't block a
    /// whole location.
    /// </summary>
    internal static string? ReviewPrefilterReason(string text)
    {
        if (text.Length > ReviewMaxChars) return "too-long";
        if (CountReviewWords(text) < ReviewMinWords) return "too-short";
        if (HasReviewContactDetails(text)) return "contact-details";
        if (_reviewMachineTranslation.IsMatch(text)) return "machine-translated";
        if (_reviewHtml.IsMatch(text) || HasReviewControlChars(text)) return "markup-or-control-characters";
        return null;
    }

    internal static string? ExtractGooglePlaceId(string? directLink)
    {
        if (string.IsNullOrWhiteSpace(directLink)) return null;

        var match = Regex.Match(directLink, @"[?&]placeid=([^&#]+)", RegexOptions.IgnoreCase);

        return match.Success ? WebUtility.UrlDecode(match.Groups[1].Value) : null;
    }

    // ---- Building and checking the stored value -----------------------------------------------------

    /// <summary>
    /// Builds the googleBusinessReviewSnapshot value from Uberall's data and the model's verdicts,
    /// then runs the QA checks. Returns null, with the reasons in <paramref name="problems"/>, when the
    /// location must keep its current value.
    /// </summary>
    internal static JObject? BuildGoogleReviewSnapshotField(JToken modelResult, ReviewLocation review, string model,
        IReadOnlySet<string> excludedIds, DateTime nowUtc, List<string> problems, List<string> softFlags)
    {
        if (review.Status != ReviewLocationStatus.Ready)
        {
            problems.Add($"not written: {review.Detail}");
            return null;
        }

        var verdicts = ParseReviewVerdicts(modelResult);

        var candidatesById = review.Candidates.ToDictionary(c => c.Id);

        var chosen = review.Candidates
            .Where(c => verdicts.TryGetValue(c.Id, out var v) && v.Eligible && v.Reasons.Count == 0 && IsReviewLanguageCode(v.Language))
            .OrderByDescending(c => c.PublishedAt ?? DateTime.MinValue)
            .ThenByDescending(c => c.Id, StringComparer.Ordinal)
            .Take(review.Score.ShowScore ? ReviewMaxTestimonials : 0)
            .ToList();

        var testimonials = new JArray(chosen.Select(c => new JObject
        {
            ["id"] = c.Id,
            ["rating"] = c.Rating,
            ["text"] = c.Text,
            ["language"] = verdicts[c.Id].Language,
            ["author"] = c.Author,
            ["publishedAt"] = FormatReviewDate(c.PublishedAt),
            ["directLink"] = c.DirectLink,
            ["evaluation"] = new JObject
            {
                ["eligible"] = true,
                ["reasons"] = new JArray(),
                ["checkedBy"] = new JArray("prefilter", "model"),
            },
        }));

        var snapshot = new JObject
        {
            ["source"] = ReviewSource,
            ["fetchedAt"] = FormatReviewDate(review.FetchedAt),
            ["uberallId"] = review.UberallId,
            ["googlePlaceId"] = review.GooglePlaceId,
            ["average"] = review.Score.Average,
            ["count"] = review.Score.Count,
            ["showScore"] = review.Score.ShowScore,
            ["scoreRule"] = new JObject { ["minAverage"] = (int)ReviewMinAverage, ["minCount"] = ReviewMinCount },
            ["testimonials"] = testimonials,
            ["selectionRule"] = ReviewSelectionRule,
            ["evaluatedAt"] = FormatReviewDate(nowUtc),
            ["model"] = model,
        };

        problems.AddRange(ValidateGoogleReviewSnapshot(snapshot, excludedIds, nowUtc, softFlags));

        if (problems.Count > 0) return null;

        // A review id can only be quoted once across the run.
        foreach (var c in chosen)
        {
            var owner = _testimonialIdsInRun.GetOrAdd(c.Id, review.LocationNumber);
            if (owner != review.LocationNumber)
            {
                problems.Add($"review {c.Id} is already quoted for location {owner}");
            }
        }

        return problems.Count > 0 ? null : snapshot;
    }

    [ScriptMemberIgnore]
    public static JObject? BuildGoogleReviewSnapshotField(JToken modelResult, JObject entry, string model,
        List<string> problems, List<string> softFlags)
    {
        var review = GetReviewLocation(entry);

        return BuildGoogleReviewSnapshotField(modelResult, review, model, ReviewExclusionIds(entry), DateTime.UtcNow, problems, softFlags);
    }

    internal static Dictionary<string, ReviewVerdict> ParseReviewVerdicts(JToken modelResult)
    {
        var items = modelResult switch
        {
            JArray a => a,
            JObject o => o["reviews"] as JArray ?? o["verdicts"] as JArray ?? o["results"] as JArray
                ?? o.Properties().Select(p => p.Value).OfType<JArray>().FirstOrDefault() ?? [],
            _ => [],
        };

        var verdicts = new Dictionary<string, ReviewVerdict>();

        foreach (var item in items.OfType<JObject>())
        {
            var id = item["id"]?.ToString().Trim();
            if (string.IsNullOrEmpty(id) || verdicts.ContainsKey(id)) continue;

            var eligible = item["eligible"]?.Type == JTokenType.Boolean && item["eligible"]!.Value<bool>();

            var reasons = (item["reasons"] as JArray ?? [])
                .Select(r => r.ToString().Trim())
                .Where(r => r.Length > 0)
                .ToList();

            var language = item["language"]?.ToString().Trim().ToLowerInvariant() ?? string.Empty;

            verdicts[id] = new ReviewVerdict(id, eligible, reasons, language);
        }

        return verdicts;
    }

    internal static bool IsReviewLanguageCode(string? code) =>
        !string.IsNullOrEmpty(code) && _reviewLanguageCode.IsMatch(code);

    /// <summary>The QA gate from the process spec, step 6. Returns the hard failures; soft flags go to <paramref name="softFlags"/>.</summary>
    internal static List<string> ValidateGoogleReviewSnapshot(JObject snapshot, IReadOnlySet<string> excludedIds,
        DateTime nowUtc, List<string> softFlags)
    {
        var errors = new List<string>();

        if (snapshot["source"]?.ToString() != ReviewSource) errors.Add("source differs from the rule");
        if (snapshot["selectionRule"]?.ToString() != ReviewSelectionRule) errors.Add("selectionRule differs from the rule");

        var countToken = snapshot["count"];
        var count = countToken?.Type == JTokenType.Integer ? countToken.Value<long>() : -1;
        if (count < 0) errors.Add("count is not an integer >= 0");

        var averageToken = snapshot["average"];
        var average = averageToken?.Type is JTokenType.Float or JTokenType.Integer ? averageToken.Value<double>() : (double?)null;
        if (count > 0 && (average is null || average < 1 || average > 5)) errors.Add("average is outside 1-5");

        var showScore = snapshot["showScore"]?.Type == JTokenType.Boolean && snapshot["showScore"]!.Value<bool>();
        var expectedShow = count >= ReviewMinCount && average is not null && average >= ReviewMinAverage;
        // The rule uses the unrounded mean and the stored average is rounded to 2 decimals, so a
        // stored 4.00 can be either side of the line (3.995 to 4.004).
        if (showScore != expectedShow && !(average == 4.0 && count >= ReviewMinCount))
        {
            errors.Add("showScore disagrees with the 4.0 / 3 rule");
        }

        var placeId = snapshot["googlePlaceId"];
        if (placeId is not null && placeId.Type != JTokenType.Null && !_googlePlaceId.IsMatch(placeId.ToString()))
        {
            errors.Add("googlePlaceId is malformed");
        }

        var testimonials = snapshot["testimonials"] as JArray;
        if (testimonials is null)
        {
            errors.Add("testimonials is missing");
            return errors;
        }

        if (testimonials.Count > 0 && !showScore) errors.Add("testimonials present while showScore is false");
        if (testimonials.Count > ReviewMaxTestimonials) errors.Add($"more than {ReviewMaxTestimonials} testimonials");

        var seen = new HashSet<string>();

        foreach (var quote in testimonials.OfType<JObject>())
        {
            var id = quote["id"]?.ToString() ?? string.Empty;
            var label = $"quote {id}";
            var text = quote["text"]?.ToString() ?? string.Empty;

            if (!seen.Add(id)) errors.Add($"{label}: id repeats");
            if (excludedIds.Contains(id)) errors.Add($"{label}: on the take-down list");

            var rating = quote["rating"]?.Type == JTokenType.Integer ? quote["rating"]!.Value<int>() : 0;
            if (rating is not (4 or 5)) errors.Add($"{label}: rating is not 4 or 5");

            if (string.IsNullOrWhiteSpace(text)) errors.Add($"{label}: text is empty");
            else if (CountReviewWords(text) < ReviewMinWords) errors.Add($"{label}: under {ReviewMinWords} words");
            if (text.Length > ReviewMaxChars) errors.Add($"{label}: over {ReviewMaxChars} characters");
            if (HasReviewContactDetails(text)) errors.Add($"{label}: contains contact details or a link");
            if (_reviewMachineTranslation.IsMatch(text)) errors.Add($"{label}: carries Google's machine translation");
            if (HasReviewControlChars(text)) errors.Add($"{label}: contains control or direction characters");
            if (_reviewHtml.IsMatch(text)) errors.Add($"{label}: contains HTML");

            if (!IsReviewLanguageCode(quote["language"]?.ToString())) errors.Add($"{label}: language is not an ISO code");
            if (string.IsNullOrWhiteSpace(quote["author"]?.ToString())) errors.Add($"{label}: no author");

            var link = quote["directLink"]?.ToString();
            if (link is null || !link.StartsWith("https://", StringComparison.Ordinal)) errors.Add($"{label}: directLink is not https://");

            var published = ParseReviewDate(quote["publishedAt"]?.ToString());
            if (published is null) errors.Add($"{label}: publishedAt is missing");
            else if (published > nowUtc.AddMinutes(5)) errors.Add($"{label}: publishedAt is in the future");

            var evaluation = quote["evaluation"] as JObject;
            var passed = evaluation?["eligible"]?.Type == JTokenType.Boolean && evaluation["eligible"]!.Value<bool>()
                && (evaluation["reasons"] as JArray)?.Count == 0
                && (evaluation["checkedBy"] as JArray)?.Any(c => c.ToString() == "model") == true;
            if (!passed) errors.Add($"{label}: not passed by the model check");

            if (text.Length > ReviewSoftMaxChars) softFlags.Add($"{label}: over {ReviewSoftMaxChars} characters");
            if (IsAllCapitals(text)) softFlags.Add($"{label}: all in capitals");
        }

        return errors;
    }

    private static bool IsAllCapitals(string text)
    {
        var cased = text.Where(c => char.IsLetter(c) && char.ToUpperInvariant(c) != char.ToLowerInvariant(c)).ToList();

        return cased.Count >= 10 && cased.All(char.IsUpper);
    }

    /// <summary>True when the stored value already says the same, ignoring fetchedAt and evaluatedAt.</summary>
    [ScriptMemberIgnore]
    public static bool GoogleReviewSnapshotsMatch(JToken? current, JToken proposed)
    {
        if (current is null || current.Type == JTokenType.Null) return false;

        return JToken.DeepEquals(NormaliseSnapshot(current), NormaliseSnapshot(proposed));

        static JToken NormaliseSnapshot(JToken token)
        {
            // Contentful's SDK may hand back dates as DateTime values; compare as plain JSON text.
            var json = JsonConvert.SerializeObject(token, new JsonSerializerSettings
            {
                DateFormatString = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            });

            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };

            var normalised = JToken.ReadFrom(reader);

            if (normalised is JObject o)
            {
                o.Remove("fetchedAt");
                o.Remove("evaluatedAt");
            }

            return normalised;
        }
    }

    // ---- Uberall ------------------------------------------------------------------------------------

    private static IReadOnlyDictionary<string, UberallLocationMatch> GetUberallLocations()
    {
        lock (_reviewRunLock)
        {
            return _uberallLocations ??= FetchUberallLocations().GetAwaiter().GetResult();
        }
    }

    internal static async Task<IReadOnlyDictionary<string, UberallLocationMatch>> FetchUberallLocations()
    {
        var byIdentifier = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        const int pageSize = 50;

        for (var offset = 0; ; offset += pageSize)
        {
            var json = await GetUberall($"/api/locations?max={pageSize}&offset={offset}");

            var response = json["response"] as JObject;
            var locations = response?["locations"] as JArray ?? [];

            foreach (var location in locations.OfType<JObject>())
            {
                var identifier = location["identifier"]?.ToString().Trim();
                var id = location["id"]?.Type == JTokenType.Integer ? location["id"]!.Value<long>() : (long?)null;

                if (string.IsNullOrEmpty(identifier) || id is null) continue;

                if (!byIdentifier.TryGetValue(identifier, out var ids)) byIdentifier[identifier] = ids = [];
                if (!ids.Contains(id.Value)) ids.Add(id.Value);
            }

            var total = response?["count"]?.Type == JTokenType.Integer ? response["count"]!.Value<int>() : (int?)null;

            if (locations.Count < pageSize || (total is not null && offset + pageSize >= total)) break;
        }

        return byIdentifier.ToDictionary(kv => kv.Key, kv => new UberallLocationMatch(kv.Key, kv.Value), StringComparer.Ordinal);
    }

    internal static async Task<UberallReviewFetch> FetchUberallReviews(long uberallId)
    {
        const int pageSize = 50;
        const int maxPages = 400;

        var reviews = new Dictionary<string, UberallReview>(StringComparer.Ordinal);
        var seenPointIds = new HashSet<string>(StringComparer.Ordinal);
        var reported = 0;

        for (var page = 1; page <= maxPages; page++)
        {
            var json = await GetUberall($"/api/datapoints?locationIds={uberallId}&dataPointTypes=REVIEW&directoryTypes=GOOGLE&max={pageSize}&page={page}");

            var inbox = json.SelectToken("response.inbox") as JObject
                ?? throw new UberallApiException(200, "response.inbox missing from the datapoints response");

            if (inbox["count"]?.Type == JTokenType.Integer) reported = inbox["count"]!.Value<int>();

            var points = inbox["dataPoints"] as JArray ?? [];

            foreach (var point in points.OfType<JObject>())
            {
                var pointId = point["id"]?.ToString().Trim();
                if (string.IsNullOrEmpty(pointId)) continue;

                seenPointIds.Add(pointId);

                // Points without an integer 1-5 rating are not reviews to score, but they still
                // count towards the fetch being complete.
                var review = ToUberallReview(point);
                if (review is not null) reviews.TryAdd(review.Id, review);
            }

            if (points.Count == 0 || seenPointIds.Count >= reported) break;
        }

        return new UberallReviewFetch(reviews.Values.ToList(), reported, seenPointIds.Count >= reported);
    }

    internal static UberallReview? ToUberallReview(JObject point)
    {
        var id = point["id"]?.ToString().Trim();
        if (string.IsNullOrEmpty(id)) return null;

        var ratingToken = point["rating"];
        var rating = ratingToken?.Type switch
        {
            JTokenType.Integer => ratingToken.Value<long>(),
            JTokenType.Float when ratingToken.Value<double>() % 1 == 0 => (long)ratingToken.Value<double>(),
            _ => 0,
        };

        // Only integer ratings 1-5 are kept.
        if (rating is < 1 or > 5) return null;

        var published = ParseReviewDate(point["actionDate"]?.ToString()) ?? ParseReviewDate(point["dateCreated"]?.ToString());

        return new UberallReview(
            id,
            (int)rating,
            CleanReviewText(point["data"]?.ToString()),
            point["author"]?.ToString().Trim() ?? string.Empty,
            published,
            point["directLink"]?.ToString().Trim());
    }

    private static async Task<JObject> GetUberall(string pathAndQuery)
    {
        var apiKey = UberallApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Uberall API key not configured. Set 'Cute__UberallApiKey'.");
        }

        var url = UberallBaseUrl.TrimEnd('/') + pathAndQuery;

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-API-KEY", apiKey);
            request.Headers.Add("Accept", "application/json");

            HttpResponseMessage response;
            try
            {
                response = await UberallHttpClient.SendAsync(request);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < 6)
            {
                await UberallDelay(TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, attempt))));
                continue;
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<JObject>(content, _uberallJsonSettings)
                        ?? throw new UberallApiException(status, "empty response");
                }

                // Never retry a bad or unauthorised key: it fails the whole run.
                if (status is 401 or 403)
                {
                    throw new UberallApiException(status, $"Uberall rejected the API key ({status}).");
                }

                if ((status == 429 || status >= 500) && attempt < 6)
                {
                    var wait = response.Headers.RetryAfter?.Delta
                        ?? TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, attempt)));
                    await UberallDelay(wait);
                    continue;
                }

                throw new UberallApiException(status, $"Uberall call to '{pathAndQuery.Split('?')[0]}' failed ({status}).");
            }
        }
    }

    // ---- Take-downs ---------------------------------------------------------------------------------

    /// <summary>
    /// The review ids never to show for this centre: the dataLocation field googleBusinessReviewExclusions
    /// (a list of review ids). The query must select the field, even when it is empty, so a query
    /// that forgot it can't let removed reviews back onto the site.
    /// </summary>
    internal static IReadOnlySet<string> ReviewExclusionIds(JObject entry)
    {
        if (!entry.ContainsKey(ReviewExclusionsField))
        {
            throw new InvalidOperationException($"The review job needs the query to return '{ReviewExclusionsField}' for each entry.");
        }

        return (entry[ReviewExclusionsField] as JArray ?? [])
            .Select(id => id.ToString().Trim())
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    // ---- Helpers ------------------------------------------------------------------------------------

    private static JObject ToReviewEntry(object location) => location switch
    {
        JObject o => o,
        null => throw new ArgumentNullException(nameof(location)),
        _ => JObject.FromObject(location),
    };

    private static string ReviewLocationNumber(JObject entry)
    {
        var number = entry["locationNumber"]?.ToString().Trim();

        return string.IsNullOrEmpty(number)
            ? throw new InvalidOperationException("The review job needs the query to return 'locationNumber' for each entry.")
            : number;
    }

    internal static DateTime? ParseReviewDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.UtcDateTime
            : null;
    }

    private static string? FormatReviewDate(DateTime? value) =>
        value?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    // ---- Types --------------------------------------------------------------------------------------

    internal sealed record UberallLocationMatch(string Identifier, IReadOnlyList<long> UberallIds);

    internal sealed record UberallReview(string Id, int Rating, string Text, string Author, DateTime? PublishedAt, string? DirectLink);

    internal sealed record UberallReviewFetch(IReadOnlyList<UberallReview> Reviews, int ReportedCount, bool Complete);

    internal sealed record ReviewScore(int Count, double? Average, bool ShowScore);

    internal sealed record ReviewVerdict(string Id, bool Eligible, IReadOnlyList<string> Reasons, string Language);

    internal enum ReviewLocationStatus { Ready, Unmatched, Ambiguous, Incomplete, FetchFailed }

    internal sealed record ReviewLocation(
        string LocationNumber,
        ReviewLocationStatus Status,
        string? Detail,
        long? UberallId,
        IReadOnlyList<UberallReview> Reviews,
        ReviewScore Score,
        string? GooglePlaceId,
        DateTime FetchedAt,
        IReadOnlyList<UberallReview> Candidates,
        IReadOnlyList<(UberallReview Review, string Reason)> Rejected)
    {
        public static ReviewLocation NotReady(string locationNumber, ReviewLocationStatus status, string detail, long? uberallId = null) =>
            new(locationNumber, status, detail, uberallId, [], new ReviewScore(0, null, false), null, DateTime.UtcNow, [], []);
    }

    internal sealed class UberallApiException(int statusCode, string message) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;

        public bool IsAuthFailure => StatusCode is 401 or 403;
    }
}
