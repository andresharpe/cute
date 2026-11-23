using Contentful.Core.Models;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Scriban;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using System.Net;
using System.Text;

namespace Cute.Unit.Tests;

// Uses the static Uberall settings and run state on CuteFunctions - never run in parallel.
[Collection("CuteFunctions.Uberall")]
public class CuteFunctionsReviewsTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 21, 30, 0, DateTimeKind.Utc);

    private const string Link = "https://search.google.com/local/reviews?placeid=ChIJabcdefghijklmnop";

    private static CuteFunctions.UberallReview Review(string id, int rating, int daysAgo = 1, string? text = null, string author = "Reviewer") =>
        new(id, rating, text ?? $"Review {id} says the staff were friendly and the meeting rooms were spotless.", author,
            Now.AddDays(-daysAgo), Link);

    private static HashSet<string> NoExclusions => [];

    private static CuteFunctions.ReviewLocation Location(params CuteFunctions.UberallReview[] reviews) =>
        CuteFunctions.BuildReviewLocation("98", 1234567, reviews, NoExclusions, Now);

    private static JObject Verdicts(params (string Id, bool Eligible, string[] Reasons, string Language)[] verdicts) => new()
    {
        ["reviews"] = new JArray(verdicts.Select(v => new JObject
        {
            ["id"] = v.Id,
            ["eligible"] = v.Eligible,
            ["reasons"] = new JArray(v.Reasons),
            ["language"] = v.Language,
            ["note"] = "",
        })),
    };

    private static JObject? Build(CuteFunctions.ReviewLocation location, JToken verdicts, List<string>? problems = null) =>
        CuteFunctions.BuildGoogleReviewSnapshotField(verdicts, location, "gpt-5.4", NoExclusions, Now, problems ?? [], []);

    // ---- Text -----------------------------------------------------------------------------------------

    [Fact]
    public void CleanReviewText_RemovesInvisibleCharacters_AndTidiesWhitespace_WithoutChangingWords()
    {
        var raw = "﻿  Très bien​ situé.\t Personnel‪aimable \r\n\r\n\r\n\r\n• Salles ⁦propres⁩   \r\nMerci‍👍‎  ";

        CuteFunctions.CleanReviewText(raw)
            .Should().Be("Très bien situé.  Personnelaimable\n\n• Salles propres\nMerci‍👍‎");
    }

    [Fact]
    public void CountReviewWords_CountsTwoCharactersAsAWord_ForScriptsWithoutSpaces()
    {
        CuteFunctions.CountReviewWords("Great place to work, friendly and helpful staff").Should().Be(8);
        CuteFunctions.CountReviewWords("とても快適なオフィスで、スタッフも親切です。").Should().Be(11);
        CuteFunctions.CountReviewWords("快適なオフィスです").Should().Be(4);
        CuteFunctions.CountReviewWords("👍 👍 👍").Should().Be(0);
    }

    [Theory]
    [InlineData("Lovely centre, the reception team always help us out with everything we need.", null)]
    [InlineData("Great place, very nice staff.", "too-short")]
    [InlineData("Ask Sam at sam.jones@example.com for a tour of this great building today.", "contact-details")]
    [InlineData("Book through https://example.com it is the best centre we have ever used.", "contact-details")]
    [InlineData("Book through www.example.com it is the best centre we have ever used.", "contact-details")]
    [InlineData("Call them on +44 20 7946 0958 and they will sort out a tour for you quickly.", "contact-details")]
    [InlineData("Ring 020 7946 0958 and they will sort out a tour for you quickly, great team.", "contact-details")]
    [InlineData("Over 2000 members and 150 desks, the team really makes this a great place.", null)]
    [InlineData("We moved in on 12/03/2024 and it has been a great place to work ever since.", null)]
    [InlineData("(Translated by Google) Very good office with friendly staff and a nice view (Original) Muy buena", "machine-translated")]
    [InlineData("Very good office with <b>friendly</b> staff and a nice view of the river.", "markup-or-control-characters")]
    public void ReviewPrefilterReason_AppliesTheDeterministicRules(string text, string? expected)
    {
        CuteFunctions.ReviewPrefilterReason(text).Should().Be(expected);
    }

    [Fact]
    public void ReviewPrefilterReason_RejectsTextOver1200Characters()
    {
        CuteFunctions.ReviewPrefilterReason(string.Join(' ', Enumerable.Repeat("great", 250))).Should().Be("too-long");
    }

    // ---- Score and candidates ---------------------------------------------------------------------------

    [Fact]
    public void ComputeReviewScore_UsesEveryReview_AndTheUnroundedMean()
    {
        var score = CuteFunctions.ComputeReviewScore([Review("1", 5), Review("2", 4), Review("3", 3), Review("4", 1), Review("5", 5), Review("6", 5)]);

        score.Count.Should().Be(6);
        score.Average.Should().Be(3.83);
        score.ShowScore.Should().BeFalse();

        CuteFunctions.ComputeReviewScore([Review("1", 4), Review("2", 4), Review("3", 4)]).ShowScore.Should().BeTrue();
        CuteFunctions.ComputeReviewScore([Review("1", 5), Review("2", 5)]).ShowScore.Should().BeFalse();
        CuteFunctions.ComputeReviewScore([]).Should().Be(new CuteFunctions.ReviewScore(0, null, false));
    }

    [Fact]
    public void BuildReviewLocation_ChecksThe40MostRecentGoodReviews_WithoutTakeDowns()
    {
        var reviews = Enumerable.Range(1, 50).Select(i => Review($"r{i}", 5, daysAgo: i))
            .Append(Review("low", 2, daysAgo: 0))
            .ToList();

        var exclusions = new HashSet<string> { "r1" };

        var location = CuteFunctions.BuildReviewLocation("98", 1234567, reviews, exclusions, Now);

        location.Score.Count.Should().Be(51);
        location.Candidates.Should().HaveCount(39);
        location.Candidates.Select(c => c.Id).Should().NotContain(["r1", "low", "r41"]);
        location.Rejected.Should().ContainSingle(r => r.Review.Id == "r1" && r.Reason == "take-down");
        location.GooglePlaceId.Should().Be("ChIJabcdefghijklmnop");
    }

    [Fact]
    public void BuildReviewLocation_HasNoCandidates_WhenTheScoreIsHidden()
    {
        var location = Location(Review("1", 5), Review("2", 1), Review("3", 2));

        location.Score.ShowScore.Should().BeFalse();
        location.Candidates.Should().BeEmpty();
    }

    // ---- Snapshot -------------------------------------------------------------------------------------

    [Fact]
    public void BuildSnapshot_KeepsTheNewestFiveApprovedQuotes_FromUberallsData()
    {
        var location = Location(Enumerable.Range(1, 8).Select(i => Review($"r{i}", i % 2 == 0 ? 5 : 4, daysAgo: i)).ToArray());

        var verdicts = Verdicts(
            ("r1", true, [], "en"),
            ("r2", false, ["mixed-or-negative"], "en"),
            ("r3", true, ["contract-or-billing"], "en"),
            ("r4", true, [], "fr"),
            ("r5", true, [], "en"),
            ("r6", true, [], "EN"),
            ("r7", true, [], "english"),
            ("r8", true, [], "nl"),
            ("unknown", true, [], "en"));

        var problems = new List<string>();
        var snapshot = Build(location, verdicts, problems)!;

        problems.Should().BeEmpty();
        snapshot.Properties().Select(p => p.Name).Should().Equal(
            "source", "fetchedAt", "uberallId", "googlePlaceId", "average", "count", "showScore", "scoreRule",
            "testimonials", "selectionRule", "evaluatedAt", "model");

        snapshot["source"]!.ToString().Should().Be("Google reviews via Uberall");
        snapshot["count"]!.Value<int>().Should().Be(8);
        snapshot["average"]!.Value<double>().Should().Be(4.5);
        snapshot["showScore"]!.Value<bool>().Should().BeTrue();
        snapshot["model"]!.ToString().Should().Be("gpt-5.4");

        var quotes = (JArray)snapshot["testimonials"]!;
        quotes.Select(q => q["id"]!.ToString()).Should().Equal("r1", "r4", "r5", "r6", "r8");
        quotes[0]["author"]!.ToString().Should().Be("Reviewer");
        quotes[0]["rating"]!.Value<int>().Should().Be(4);
        quotes[1]["language"]!.ToString().Should().Be("fr");
        quotes[3]["language"]!.ToString().Should().Be("en");
        quotes[0]["publishedAt"]!.ToString().Should().Be("2026-10-31T21:30:00.000Z");
        quotes[0]["evaluation"]!.ToString(Newtonsoft.Json.Formatting.None)
            .Should().Be("""{"eligible":true,"reasons":[],"checkedBy":["prefilter","model"]}""");
    }

    [Fact]
    public void BuildSnapshot_ShowsNoQuotes_WhenTheScoreIsHidden()
    {
        var snapshot = Build(Location(Review("1", 5), Review("2", 1)), Verdicts(("1", true, [], "en")))!;

        snapshot["showScore"]!.Value<bool>().Should().BeFalse();
        ((JArray)snapshot["testimonials"]!).Should().BeEmpty();
    }

    [Fact]
    public void BuildSnapshot_NeverShowsAReviewWithoutAVerdict()
    {
        var snapshot = Build(Location(Review("1", 5), Review("2", 5), Review("3", 5)), new JObject { ["reviews"] = new JArray() })!;

        ((JArray)snapshot["testimonials"]!).Should().BeEmpty();
    }

    [Fact]
    public void BuildSnapshot_RejectsAQuoteAlreadyUsedByAnotherLocationInTheRun()
    {
        CuteFunctions.StartGoogleReviewRun();

        var reviews = new[] { Review("shared", 5), Review("2", 5), Review("3", 5) };
        var verdicts = Verdicts(("shared", true, [], "en"));

        Build(CuteFunctions.BuildReviewLocation("98", 1, reviews, NoExclusions, Now), verdicts).Should().NotBeNull();

        var problems = new List<string>();
        Build(CuteFunctions.BuildReviewLocation("99", 2, reviews, NoExclusions, Now), verdicts, problems).Should().BeNull();
        problems.Should().ContainSingle().Which.Should().Contain("already quoted for location 98");

        CuteFunctions.StartGoogleReviewRun();
    }

    [Fact]
    public void BuildSnapshot_IsNotWritten_WhenTheFetchWasIncomplete()
    {
        var problems = new List<string>();
        var location = CuteFunctions.ReviewLocation.NotReady("98", CuteFunctions.ReviewLocationStatus.Incomplete, "incomplete fetch (40 of 87 reviews)");

        Build(location, Verdicts(), problems).Should().BeNull();
        problems.Should().ContainSingle().Which.Should().Contain("incomplete fetch");
    }

    // ---- QA gate --------------------------------------------------------------------------------------

    private static JObject GoodSnapshot() => Build(
        Location(Review("1", 5), Review("2", 5), Review("3", 4)),
        Verdicts(("1", true, [], "en"), ("2", true, [], "en")))!;

    private static List<string> Qa(JObject snapshot, List<string>? softFlags = null) =>
        CuteFunctions.ValidateGoogleReviewSnapshot(snapshot, new HashSet<string> { "gone" }, Now, softFlags ?? []);

    [Fact]
    public void Qa_PassesAGoodSnapshot()
    {
        Qa(GoodSnapshot()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("rating", 3, "rating is not 4 or 5")]
    [InlineData("author", "", "no author")]
    [InlineData("directLink", "http://search.google.com/x", "directLink is not https://")]
    [InlineData("publishedAt", "2026-12-01T00:00:00.000Z", "publishedAt is in the future")]
    [InlineData("language", "english", "language is not an ISO code")]
    [InlineData("text", "Too short to quote here.", "under 8 words")]
    [InlineData("text", "Brilliant team, call 0207 946 0958 any time for help with bookings and tours.", "contact details")]
    [InlineData("text", "Brilliant team who always <i>help</i> with everything, really recommended to all.", "HTML")]
    [InlineData("text", "Brilliant team‮ who always help with everything, really recommended to all.", "control or direction")]
    [InlineData("id", "gone", "take-down list")]
    public void Qa_FailsABadQuote(string field, object value, string expected)
    {
        var snapshot = GoodSnapshot();
        snapshot["testimonials"]![0]![field] = JToken.FromObject(value);

        Qa(snapshot).Should().Contain(e => e.Contains(expected));
    }

    [Fact]
    public void Qa_FailsSnapshotLevelRules()
    {
        var hidden = GoodSnapshot();
        hidden["showScore"] = false;
        Qa(hidden).Should().Contain(e => e.Contains("showScore disagrees")).And.Contain(e => e.Contains("testimonials present"));

        var tooMany = GoodSnapshot();
        var quote = tooMany["testimonials"]![0]!;
        for (var i = 0; i < 5; i++) ((JArray)tooMany["testimonials"]!).Add(quote.DeepClone());
        Qa(tooMany).Should().Contain(e => e.Contains("more than 5")).And.Contain(e => e.Contains("id repeats"));

        var badSource = GoodSnapshot();
        badSource["source"] = "Google";
        Qa(badSource).Should().Contain("source differs from the rule");

        var notChecked = GoodSnapshot();
        notChecked["testimonials"]![0]!["evaluation"]!["checkedBy"] = new JArray("prefilter");
        Qa(notChecked).Should().Contain(e => e.Contains("not passed by the model check"));
    }

    [Fact]
    public void Qa_FlagsLongOrShoutyQuotes_WithoutBlocking()
    {
        var snapshot = GoodSnapshot();
        snapshot["testimonials"]![0]!["text"] = "THE BEST CENTRE IN TOWN, FRIENDLY STAFF AND GREAT COFFEE EVERY DAY";

        var flags = new List<string>();
        Qa(snapshot, flags).Should().BeEmpty();
        flags.Should().ContainSingle().Which.Should().Contain("all in capitals");
    }

    // ---- Change detection -----------------------------------------------------------------------------

    [Fact]
    public void SnapshotsMatch_IgnoresOnlyTheTimestamps()
    {
        var current = GoodSnapshot();

        var rerun = (JObject)current.DeepClone();
        rerun["fetchedAt"] = "2026-12-01T21:30:00.000Z";
        rerun["evaluatedAt"] = "2026-12-01T21:31:00.000Z";
        CuteFunctions.GoogleReviewSnapshotsMatch(current, rerun).Should().BeTrue();

        var changed = (JObject)rerun.DeepClone();
        changed["count"] = 4;
        CuteFunctions.GoogleReviewSnapshotsMatch(current, changed).Should().BeFalse();

        // Contentful's SDK can return dates as DateTime values.
        var fromSdk = (JObject)current.DeepClone();
        fromSdk["testimonials"]![0]!["publishedAt"] = new JValue(new DateTime(2026, 10, 31, 21, 30, 0, DateTimeKind.Utc));
        CuteFunctions.GoogleReviewSnapshotsMatch(fromSdk, current).Should().BeTrue();

        CuteFunctions.GoogleReviewSnapshotsMatch(null, current).Should().BeFalse();
    }

    // ---- Uberall --------------------------------------------------------------------------------------

    private sealed class FakeUberall(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string Page(int page, int count, params (string Id, object Rating)[] points) => new JObject
    {
        ["status"] = "SUCCESS",
        ["response"] = new JObject
        {
            ["inbox"] = new JObject
            {
                ["page"] = page,
                ["pageSize"] = 50,
                ["count"] = count,
                ["dataPoints"] = new JArray(points.Select(p => new JObject
                {
                    ["id"] = long.Parse(p.Id),
                    ["rating"] = JToken.FromObject(p.Rating),
                    ["data"] = $"Point {p.Id}\tis a lovely place to work with great staff.",
                    ["author"] = "Reviewer name",
                    ["actionDate"] = "2026-04-18T10:56:42.000+00:00",
                    ["directLink"] = Link,
                })),
            },
        },
    }.ToString();

    private static FakeUberall UseUberall(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var fake = new FakeUberall(respond);
        CuteFunctions.UberallHttpClient = new HttpClient(fake);
        CuteFunctions.UberallApiKey = "test-key";
        CuteFunctions.UberallBaseUrl = "https://uberall.test";
        CuteFunctions.UberallDelay = _ => Task.CompletedTask;
        CuteFunctions.StartGoogleReviewRun();
        return fake;
    }

    [Fact]
    public async Task FetchUberallReviews_PagesWithPageAndDataPointTypes_AndIsCompleteAtCount()
    {
        var fake = UseUberall(r => r.RequestUri!.Query.Contains("page=1")
            ? Json(Page(1, 3, ("101", 5), ("102", 4)))
            : Json(Page(2, 3, ("102", 4), ("103", 2.0))));

        var fetch = await CuteFunctions.FetchUberallReviews(1234567);

        fetch.Complete.Should().BeTrue();
        fetch.Reviews.Select(r => r.Id).Should().BeEquivalentTo(["101", "102", "103"]);
        fetch.Reviews[0].Text.Should().Be("Point 101 is a lovely place to work with great staff.");
        fetch.Reviews[0].PublishedAt.Should().Be(new DateTime(2026, 4, 18, 10, 56, 42, DateTimeKind.Utc));

        fake.Requests.Should().HaveCount(2);
        fake.Requests[0].RequestUri!.Query.Should().Contain("dataPointTypes=REVIEW").And.Contain("directoryTypes=GOOGLE")
            .And.Contain("locationIds=1234567").And.NotContain("offset");
        fake.Requests[0].Headers.GetValues("X-API-KEY").Should().Equal("test-key");
    }

    [Fact]
    public async Task FetchUberallReviews_IsIncomplete_WhenFewerPointsArriveThanTheCount()
    {
        UseUberall(r => r.RequestUri!.Query.Contains("page=1") ? Json(Page(1, 5, ("101", 5))) : Json(Page(2, 5)));

        (await CuteFunctions.FetchUberallReviews(1)).Complete.Should().BeFalse();
    }

    [Fact]
    public async Task FetchUberallReviews_RetriesThrottling_ButNeverABadKey()
    {
        var calls = 0;
        UseUberall(_ => ++calls == 1 ? Json("{}", HttpStatusCode.TooManyRequests) : Json(Page(1, 1, ("101", 5))));
        (await CuteFunctions.FetchUberallReviews(1)).Reviews.Should().ContainSingle();
        calls.Should().Be(2);

        var fake = UseUberall(_ => Json("{}", HttpStatusCode.Unauthorized));
        var act = () => CuteFunctions.FetchUberallReviews(1);
        await act.Should().ThrowAsync<Exception>().WithMessage("*rejected the API key*");
        fake.Requests.Should().ContainSingle();
    }

    private static string Locations(params (long Id, string Identifier)[] locations) => new JObject
    {
        ["response"] = new JObject
        {
            ["count"] = locations.Length,
            ["locations"] = new JArray(locations.Select(l => new JObject { ["id"] = l.Id, ["identifier"] = l.Identifier })),
        },
    }.ToString();

    [Fact]
    public void SkipReason_LeavesClosedHiddenUnmatchedAndAmbiguousLocationsUntouched()
    {
        UseUberall(r => r.RequestUri!.AbsolutePath == "/api/locations"
            ? Json(Locations((1, " 98 "), (2, "500"), (3, "500")))
            : Json(Page(1, 3, ("101", 5), ("102", 5), ("103", 4))));

        CuteFunctions.GetGoogleReviewSkipReason(JObject.Parse("""{ "locationNumber": 98, "googleBusinessReviewExclusions": null }"""), Now).Should().BeNull();
        CuteFunctions.GetGoogleReviewSkipReason(JObject.Parse("""{ "locationNumber": 98, "isVisibleOnWebsites": false, "googleBusinessReviewExclusions": null }"""), Now).Should().Be("not visible on websites");
        CuteFunctions.GetGoogleReviewSkipReason(JObject.Parse("""{ "locationNumber": 98, "closeDate": "2026-11-01", "googleBusinessReviewExclusions": null }"""), Now).Should().Be("closed on 2026-11-01");
        CuteFunctions.GetGoogleReviewSkipReason(JObject.Parse("""{ "locationNumber": 98, "closeDate": "2027-01-31", "googleBusinessReviewExclusions": null }"""), Now).Should().BeNull();
        CuteFunctions.GetGoogleReviewSkipReason(JObject.Parse("""{ "locationNumber": 7, "googleBusinessReviewExclusions": null }"""), Now).Should().Be("not in Uberall");
        CuteFunctions.GetGoogleReviewSkipReason(JObject.Parse("""{ "locationNumber": 500, "googleBusinessReviewExclusions": null }"""), Now).Should().StartWith("Uberall identifier on 2 locations");
    }

    [Fact]
    public void ReviewExclusionIds_RequiresTheQueryToReturnTheField()
    {
        CuteFunctions.ReviewExclusionIds(JObject.Parse("""{ "googleBusinessReviewExclusions": [" 1 ", "", "2"] }"""))
            .Should().BeEquivalentTo(["1", "2"]);
        CuteFunctions.ReviewExclusionIds(JObject.Parse("""{ "googleBusinessReviewExclusions": null }""")).Should().BeEmpty();

        var act = () => CuteFunctions.ReviewExclusionIds(JObject.Parse("""{ "locationNumber": 98 }"""));
        act.Should().Throw<InvalidOperationException>().WithMessage("*googleBusinessReviewExclusions*");
    }

    // ---- Prompt functions -----------------------------------------------------------------------------

    private static string Render(string template, JObject location)
    {
        var scriptObject = new ScriptObject();
        scriptObject.SetValue("cute", new CuteFunctions(), true);
        scriptObject.SetValue("dataLocation", location, true);

        return Template.Parse(template).Render(scriptObject, memberRenamer: m => char.ToLowerInvariant(m.Name[0]) + m.Name[1..]);
    }

    [Fact]
    public void GoogleReviews_SendsOnlyIdDateAndText_AndSkipsTakeDowns()
    {
        UseUberall(r => r.RequestUri!.AbsolutePath == "/api/locations"
            ? Json(Locations((1, "98")))
            : Json(Page(1, 4, ("101", 5), ("102", 4), ("750839557", 5), ("104", 3))));

        var output = Render("{{ cute.google_reviews dataLocation }}",
            JObject.Parse("""{ "locationNumber": 98, "googleBusinessReviewExclusions": ["750839557"] }"""));

        var lines = output.Split('\n').Select(JObject.Parse).ToList();
        lines.Select(l => l["id"]!.ToString()).Should().BeEquivalentTo(["101", "102"]);
        lines.Should().AllSatisfy(l => l.Properties().Select(p => p.Name).Should().Equal("id", "date", "text"));
        lines[0]["date"]!.ToString().Should().Be("2026-04-18");
        output.Should().NotContain("Reviewer name");
    }

    [Fact]
    public void ReviewBuildingFacts_ListsTheBuildingOnly()
    {
        var location = JObject.Parse("""
        {
          "name": "Paris Opéra", "address1": "1 rue Auber", "address2": null, "zipPostCode": "75009", "city": "Paris",
          "isTwentyFourSevenAccessible": true,
          "dataLocationAmenityEntriesCollection": { "items": [ { "name": "Showers" }, { "name": "Café" }, { "name": "Showers" } ] },
          "transitInformation": "Opéra Metro station: 100 m\nGare Saint-Lazare railway station: 800 m\n"
        }
        """);

        Render("{{ cute.review_building_facts dataLocation }}", location).Should().Be(
            "BUILDING FACTS (from the CMS)\nname: Paris Opéra\naddress: 1 rue Auber, 75009, Paris\n24/7 access: yes\n" +
            "amenities: Showers; Café\nnearby transport: Opéra Metro station: 100 m; Gare Saint-Lazare railway station: 800 m");
    }

    [Fact]
    public void ReviewBuildingFacts_PrefersNearbyTransit()
    {
        var location = JObject.Parse("""
        {
          "name": "Manchester King Street", "address1": "82 King Street", "city": "Manchester",
          "transitInformation": "Old line",
          "nearbyTransit": { "items": [ { "name": "St Peter's Square", "type": "tram_stop" } ], "source": "openstreetmap+osrm" }
        }
        """);

        Render("{{ cute.review_building_facts dataLocation }}", location)
            .Should().EndWith("nearby transport: St Peter's Square (tram stop)");
    }

    [Fact]
    public void EnsureFieldExistsOrAdd_KeepsTheKeyFilter_WhenTheQueryAlreadyHasTheField()
    {
        var query = "query Q($skip: Int) { dataLocationCollection(skip: $skip, where: { isVisibleOnWebsites: true }) { items { key googleBusinessReviewSnapshot } } }";

        Cute.Lib.Contentful.GraphQL.GraphQLUtilities.EnsureFieldExistsOrAdd(query, "googleBusinessReviewSnapshot", "France.Nice.6106")
            .Should().Contain("key: \"France.Nice.6106\"").And.Contain("isVisibleOnWebsites: true");
    }

    [Fact]
    public void ReviewTransitItems_ReadsJsonTransitAndCapsAtTen()
    {
        var items = new JArray(Enumerable.Range(1, 12).Select(i => new JObject { ["name"] = $"Stop {i}", ["type"] = "subway_station" }));

        var listed = CuteFunctions.ReviewTransitItems(items);

        listed.Should().HaveCount(10);
        listed[0].Should().Be("Stop 1 (subway station)");
    }

    // ---- Writing --------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5, 4, null, true)]
    [InlineData(6, 4, null, false)]
    [InlineData(1, null, null, false)]
    [InlineData(5, 4, 5, false)]
    public void IsCleanlyPublished_RefusesDraftsAndArchivedEntries(int version, int? published, int? archived, bool expected)
    {
        var sys = new SystemProperties { Version = version, PublishedVersion = published, ArchivedVersion = archived };

        GenerateBulkAction.IsCleanlyPublished(sys, out _).Should().Be(expected);
    }
}
