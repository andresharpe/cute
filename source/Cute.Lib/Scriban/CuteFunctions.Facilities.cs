using Cute.Lib.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scriban.Runtime;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Cute.Lib.Scriban;

/// <summary>
/// Google-backed lookup of cafes, restaurants, bars and gyms for the
/// 'DataLocation.AllLocations.NearestFacilities' generate job.
///
/// Google supplies every fact (place id, name, rating, address, coordinates, distances). The model
/// only chooses the best 5 and returns place ids; <see cref="BuildNearestFacilitiesField"/> then
/// builds the stored JSON from Google's data and adds walking routes for the chosen places only.
///
/// Requires 'Cute__GoogleApiKey' with Places API (New) and Routes API enabled. Rating and review
/// count make these searches bill as Nearby Search Enterprise.
/// </summary>
public partial class CuteFunctions
{
    /// <summary>1 mile. Hard upper limit for the search radius (straight-line).</summary>
    internal const int FacilityMaxRadiusMeters = 1609;

    /// <summary>Most facilities stored per centre.</summary>
    internal const int FacilityMaxResults = 5;

    internal static readonly FacilityCategory[] FacilityCategories =
    [
        new("cafe", "CAFES", ["cafe", "coffee_shop"], ["cafe"]),
        new("restaurant", "RESTAURANTS", ["restaurant"], ["restaurant"]),
        new("bar", "BARS", ["bar", "pub", "wine_bar"], ["bar"]),
        new("gym", "GYMS", ["gym", "fitness_center"], ["gym"]),
    ];

    private static readonly ConcurrentDictionary<string, FacilitySearchResult> _facilityCache = [];

    private static readonly ConcurrentDictionary<string, IReadOnlyList<WalkingRoute?>> _facilityWalkingCache = [];

    // Categories whose full type list Google rejected - use the safe list for the rest of the run.
    private static readonly ConcurrentDictionary<string, bool> _facilityTypesRejected = [];

    /// <summary>
    /// Returns a plain-text list of cafes, restaurants, bars and gyms near a point, most popular first
    /// per type, with Google place ids, ratings, review counts and straight-line distances.
    /// Scriban: {{ cute.nearby_facilities lat lon }}
    /// </summary>
    /// <param name="latitude">Latitude of the centre.</param>
    /// <param name="longitude">Longitude of the centre.</param>
    /// <param name="radiusMeters">Search radius (default and maximum 1609 m = 1 mile).</param>
    /// <param name="languageCode">Optional Google language code for names and addresses. Empty = Google default.</param>
    public static string NearbyFacilities(object latitude, object longitude, int radiusMeters = FacilityMaxRadiusMeters, string? languageCode = null)
    {
        var lat = ToCoordinate(latitude, nameof(latitude));
        var lon = ToCoordinate(longitude, nameof(longitude));

        return FormatFacilityBriefing(GetFacilityCandidates(lat, lon, radiusMeters, languageCode));
    }

    [ScriptMemberIgnore]
    internal static FacilitySearchResult GetFacilityCandidates(double lat, double lon, int radiusMeters = FacilityMaxRadiusMeters, string? languageCode = null)
    {
        radiusMeters = Math.Clamp(radiusMeters, 1, FacilityMaxRadiusMeters);

        var cacheKey = string.Create(CultureInfo.InvariantCulture, $"{lat:F6}|{lon:F6}|{radiusMeters}|{languageCode}");

        if (_facilityCache.TryGetValue(cacheKey, out var cached)) return cached;

        var apiKey = RequireGoogleApiKey();

        var perCategory = new List<(string Category, List<FacilityCandidate> Found)>();

        foreach (var category in FacilityCategories)
        {
            var found = SearchFacilityCategory(apiKey, lat, lon, radiusMeters, category, languageCode).GetAwaiter().GetResult();

            foreach (var f in found)
            {
                f.StraightLineMeters = (int)Math.Round(Haversine.DistanceInKm(lon, f.Lon, lat, f.Lat) * 1000);
            }

            perCategory.Add((category.Id, found));
        }

        var result = new FacilitySearchResult(lat, lon, radiusMeters, MergeFacilityCandidates(perCategory, radiusMeters));

        _facilityCache.TryAdd(cacheKey, result);

        return result;
    }

    private static string RequireGoogleApiKey()
    {
        var apiKey = GoogleApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Google API key not configured. Set 'Cute__GoogleApiKey' (Places API (New) and Routes API enabled).");
        }

        return apiKey;
    }

    private static async Task<List<FacilityCandidate>> SearchFacilityCategory(string apiKey, double lat, double lon,
        int radiusMeters, FacilityCategory category, string? languageCode)
    {
        if (_facilityTypesRejected.ContainsKey(category.Id))
        {
            return await SearchNearbyFacilities(apiKey, lat, lon, radiusMeters, category.SafeTypes, languageCode);
        }

        try
        {
            return await SearchNearbyFacilities(apiKey, lat, lon, radiusMeters, category.Types, languageCode);
        }
        catch (GoogleApiException ex) when (ex.StatusCode == 400)
        {
            _facilityTypesRejected.TryAdd(category.Id, true);
            return await SearchNearbyFacilities(apiKey, lat, lon, radiusMeters, category.SafeTypes, languageCode);
        }
    }

    private static async Task<List<FacilityCandidate>> SearchNearbyFacilities(string apiKey, double lat, double lon,
        int radiusMeters, string[] includedTypes, string? languageCode)
    {
        var body = new JObject
        {
            ["includedTypes"] = new JArray(includedTypes),
            ["maxResultCount"] = 20,
            ["rankPreference"] = "POPULARITY",
            ["locationRestriction"] = new JObject
            {
                ["circle"] = new JObject
                {
                    ["center"] = new JObject { ["latitude"] = lat, ["longitude"] = lon },
                    ["radius"] = (double)radiusMeters,
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            body["languageCode"] = languageCode;
        }

        var json = await PostGoogle(apiKey,
            "https://places.googleapis.com/v1/places:searchNearby",
            body.ToString(Formatting.None),
            "places.id,places.displayName,places.formattedAddress,places.location,places.primaryType,places.primaryTypeDisplayName,places.businessStatus,places.googleMapsUri,places.rating,places.userRatingCount,places.priceLevel");

        var places = JObject.Parse(json)["places"] as JArray ?? new JArray();

        return places
            .OfType<JObject>()
            .Where(p => p["id"]?.Value<string>() is not null && p.SelectToken("location.latitude") is not null)
            .Select(p => new FacilityCandidate
            {
                Id = p["id"]!.Value<string>()!,
                Name = p.SelectToken("displayName.text")?.Value<string>() ?? "(unnamed)",
                Address = p["formattedAddress"]?.Value<string>(),
                PrimaryType = p["primaryType"]?.Value<string>(),
                PrimaryTypeDisplay = p.SelectToken("primaryTypeDisplayName.text")?.Value<string>(),
                BusinessStatus = p["businessStatus"]?.Value<string>(),
                GoogleMapsUri = p["googleMapsUri"]?.Value<string>(),
                Rating = p["rating"]?.Value<double?>(),
                UserRatingCount = p["userRatingCount"]?.Value<int?>(),
                PriceLevel = p["priceLevel"]?.Value<string>(),
                Lat = p.SelectToken("location.latitude")!.Value<double>(),
                Lon = p.SelectToken("location.longitude")?.Value<double>() ?? lon,
            })
            .ToList();
    }

    /// <summary>
    /// Operational and inside the radius only. A place returned by several searches (a bar that is
    /// also a restaurant) is kept once, under the first category, with the others noted.
    /// Google's popularity order within each category is kept.
    /// </summary>
    internal static List<FacilityCandidate> MergeFacilityCandidates(IEnumerable<(string Category, List<FacilityCandidate> Found)> perCategory, int radiusMeters)
    {
        var merged = new List<FacilityCandidate>();
        var byId = new Dictionary<string, FacilityCandidate>();

        foreach (var (category, found) in perCategory)
        {
            foreach (var f in found)
            {
                if (f.BusinessStatus is not null && f.BusinessStatus != "OPERATIONAL") continue;
                if (f.StraightLineMeters > radiusMeters) continue;

                if (byId.TryGetValue(f.Id, out var existing))
                {
                    if (!existing.Categories.Contains(category)) existing.Categories.Add(category);
                    continue;
                }

                f.Categories.Add(category);
                byId[f.Id] = f;
                merged.Add(f);
            }
        }

        return merged;
    }

    internal static string FormatFacilityBriefing(FacilitySearchResult search)
    {
        var sb = new StringBuilder();

        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Search: Google Places within {search.RadiusMeters} m ({search.RadiusMeters / MetresPerMile:0.#} mile) straight-line of the centre, most popular first in each group."));
        sb.AppendLine("Distances are straight-line from the centre (walking is roughly 1.3x longer).");

        foreach (var category in FacilityCategories)
        {
            var items = search.Candidates.Where(c => c.Categories[0] == category.Id).ToList();

            sb.AppendLine();
            sb.Append(category.Heading).AppendLine(" (placeId | name | Google type | rating | reviews | price | distance | address | also listed as):");

            if (items.Count == 0)
            {
                sb.AppendLine("- none found");
                continue;
            }

            foreach (var c in items)
            {
                sb.Append("- ")
                  .Append(c.Id).Append(" | ")
                  .Append(c.Name).Append(" | ")
                  .Append(c.PrimaryTypeDisplay ?? c.PrimaryType ?? category.Id).Append(" | ")
                  .Append(c.Rating is null ? "no rating" : c.Rating.Value.ToString("0.0", CultureInfo.InvariantCulture)).Append(" | ")
                  .Append((c.UserRatingCount ?? 0).ToString("N0", CultureInfo.InvariantCulture)).Append(" reviews | ")
                  .Append(PriceLevelText(c.PriceLevel) ?? "price n/a").Append(" | ")
                  .Append(c.StraightLineMeters.ToString(CultureInfo.InvariantCulture)).Append(" m | ")
                  .Append(c.Address ?? "(no address)");

                if (c.Categories.Count > 1) sb.Append(" | ").Append(string.Join(", ", c.Categories.Skip(1)));

                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    // Google enum (PRICE_LEVEL_MODERATE) to a plain word (moderate). Unspecified = no value.
    internal static string? PriceLevelText(string? priceLevel) =>
        priceLevel switch
        {
            null or "" or "PRICE_LEVEL_UNSPECIFIED" => null,
            _ when priceLevel.StartsWith("PRICE_LEVEL_", StringComparison.Ordinal) =>
                priceLevel["PRICE_LEVEL_".Length..].ToLowerInvariant().Replace('_', ' '),
            _ => priceLevel.ToLowerInvariant(),
        };

    /// <summary>
    /// Builds the stored 'nearestFacilities' value from the model's choice. Only place ids Google
    /// returned are kept, every fact comes from Google, the model's order (best first) is kept and
    /// the list is capped at 5. Walking routes are fetched for the chosen places only.
    /// Accepts {"facilities":[{"placeId","category"}]} or a bare array of the same objects.
    /// </summary>
    [ScriptMemberIgnore]
    public static JObject BuildNearestFacilitiesField(JToken modelResult, double lat, double lon, DateTime? nowUtc = null)
    {
        var search = GetFacilityCandidates(lat, lon);

        return BuildNearestFacilitiesField(modelResult, search, chosen => GetWalkingRoutes(lat, lon, chosen), nowUtc ?? DateTime.UtcNow);
    }

    internal static JObject BuildNearestFacilitiesField(JToken modelResult, FacilitySearchResult search,
        Func<IReadOnlyList<FacilityCandidate>, IReadOnlyList<WalkingRoute?>> walkingRoutes, DateTime nowUtc)
    {
        var picks = modelResult switch
        {
            JObject o => o["facilities"] as JArray ?? new JArray(),
            JArray a => a,
            _ => new JArray(),
        };

        var byId = search.Candidates.ToDictionary(c => c.Id);

        var chosen = new List<(FacilityCandidate Candidate, string Category)>();

        foreach (var pick in picks.OfType<JObject>())
        {
            if (chosen.Count == FacilityMaxResults) break;

            var placeId = pick["placeId"]?.Value<string>()?.Trim();

            if (placeId is null || !byId.TryGetValue(placeId, out var candidate)) continue;

            if (chosen.Any(c => c.Candidate.Id == placeId)) continue;

            // The model may say which of the place's categories it was picked for; otherwise its first.
            var category = pick["category"]?.Value<string>()?.Trim().ToLowerInvariant();

            if (category is null || !candidate.Categories.Contains(category)) category = candidate.Categories[0];

            chosen.Add((candidate, category));
        }

        var routes = chosen.Count == 0 ? [] : walkingRoutes(chosen.Select(c => c.Candidate).ToList());

        var facilities = new JArray(chosen.Select((c, i) =>
        {
            var route = i < routes.Count ? routes[i] : null;
            var distance = route?.Meters ?? c.Candidate.StraightLineMeters;

            return new JObject
            {
                ["placeId"] = c.Candidate.Id,
                ["name"] = c.Candidate.Name,
                ["category"] = c.Category,
                ["type"] = c.Candidate.PrimaryTypeDisplay ?? c.Candidate.PrimaryType,
                ["address"] = c.Candidate.Address,
                ["lat"] = Math.Round(c.Candidate.Lat, 6),
                ["lng"] = Math.Round(c.Candidate.Lon, 6),
                ["rating"] = c.Candidate.Rating,
                ["userRatingCount"] = c.Candidate.UserRatingCount,
                ["priceLevel"] = PriceLevelText(c.Candidate.PriceLevel),
                ["googleMapsUri"] = c.Candidate.GoogleMapsUri,
                ["distanceMetres"] = distance,
                ["distanceMiles"] = Math.Round(distance / MetresPerMile, 2),
                ["distanceType"] = route is null ? "straight_line" : "walking",
                ["walkingMinutes"] = route?.Minutes,
                ["straightLineMetres"] = c.Candidate.StraightLineMeters,
            };
        }));

        return new JObject
        {
            ["source"] = "google_places",
            ["generatedAt"] = nowUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["maxRadiusMiles"] = Math.Round(search.RadiusMeters / MetresPerMile, 1),
            ["facilities"] = facilities,
        };
    }

    // Walking route from each chosen place to the centre, cached per centre and set of places.
    private static IReadOnlyList<WalkingRoute?> GetWalkingRoutes(double lat, double lon, IReadOnlyList<FacilityCandidate> places)
    {
        var cacheKey = string.Create(CultureInfo.InvariantCulture, $"{lat:F6}|{lon:F6}|{string.Join(',', places.Select(p => p.Id))}");

        if (_facilityWalkingCache.TryGetValue(cacheKey, out var cached)) return cached;

        var routes = ComputeWalkingRoutesToCentre(RequireGoogleApiKey(), lat, lon, places.Select(p => (p.Lat, p.Lon)).ToList())
            .GetAwaiter().GetResult();

        IReadOnlyList<WalkingRoute?> result = routes ?? places.Select(_ => (WalkingRoute?)null).ToList();

        _facilityWalkingCache.TryAdd(cacheKey, result);

        return result;
    }

    /// <summary>
    /// Walking route from each origin to the centre in one Routes matrix call. Returns one entry per
    /// origin (null where no route exists), or null if the call failed.
    /// </summary>
    private static async Task<IReadOnlyList<WalkingRoute?>?> ComputeWalkingRoutesToCentre(string apiKey, double lat, double lon,
        IReadOnlyList<(double Lat, double Lon)> origins)
    {
        if (origins.Count == 0) return [];

        static JObject Waypoint(double la, double lo) => new()
        {
            ["waypoint"] = new JObject
            {
                ["location"] = new JObject
                {
                    ["latLng"] = new JObject { ["latitude"] = la, ["longitude"] = lo },
                },
            },
        };

        var body = new JObject
        {
            ["origins"] = new JArray(origins.Select(o => Waypoint(o.Lat, o.Lon))),
            ["destinations"] = new JArray(Waypoint(lat, lon)),
            ["travelMode"] = "WALK",
        };

        try
        {
            var json = await PostGoogle(apiKey,
                "https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix",
                body.ToString(Formatting.None),
                "originIndex,destinationIndex,distanceMeters,duration,condition");

            var token = JToken.Parse(json);

            var elements = token as JArray ?? (token["elements"] as JArray) ?? new JArray();

            var routes = new WalkingRoute?[origins.Count];

            foreach (var element in elements.OfType<JObject>())
            {
                var index = element["originIndex"]?.Value<int?>() ?? -1;

                if (index < 0 || index >= origins.Count) continue;

                if (element["condition"]?.Value<string>() is string condition && condition != "ROUTE_EXISTS") continue;

                if (element["distanceMeters"]?.Value<int?>() is not int meters) continue;

                routes[index] = new WalkingRoute(meters, ParseDurationMinutes(element["duration"]?.Value<string>()));
            }

            return routes;
        }
        catch (GoogleApiException)
        {
            return null;
        }
    }

    internal sealed record WalkingRoute(int Meters, int? Minutes);

    internal sealed record FacilityCategory(string Id, string Heading, string[] Types, string[] SafeTypes);

    internal sealed record FacilitySearchResult(double Lat, double Lon, int RadiusMeters, IReadOnlyList<FacilityCandidate> Candidates);

    internal sealed class FacilityCandidate
    {
        public string Id { get; init; } = default!;
        public string Name { get; init; } = default!;
        public string? Address { get; init; }
        public string? PrimaryType { get; init; }
        public string? PrimaryTypeDisplay { get; init; }
        public string? BusinessStatus { get; init; }
        public string? GoogleMapsUri { get; init; }
        public double? Rating { get; init; }
        public int? UserRatingCount { get; init; }
        public string? PriceLevel { get; init; }
        public double Lat { get; init; }
        public double Lon { get; init; }
        public int StraightLineMeters { get; set; }
        public List<string> Categories { get; } = [];
    }
}
