using Cute.Lib.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scriban.Runtime;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Cute.Lib.Scriban;

/// <summary>
/// Google-backed car park lookup for the 'DataLocation.AllLocations.NearbyCarParks' generate job.
///
/// Google supplies every fact (place id, address, coordinates, distances). The model only chooses
/// which candidates are public car parks, merges duplicates and tidies names, and returns place ids.
/// <see cref="BuildNearbyCarParksField"/> then rebuilds the stored JSON from Google's data, so an
/// invented car park, address or distance can't reach Contentful.
///
/// Requires 'Cute__GoogleApiKey' with Places API (New) and Routes API enabled.
/// </summary>
public partial class CuteFunctions
{
    /// <summary>2 miles. Hard upper limit for the search radius (straight-line, as Google measures it).</summary>
    internal const int CarParkMaxRadiusMeters = 3219;

    /// <summary>Most car parks stored per centre.</summary>
    internal const int CarParkMaxResults = 5;

    /// <summary>Nearest candidates (straight-line) sent for walking routes and shown to the model.</summary>
    internal const int CarParkCandidateCount = 12;

    internal const double MetresPerMile = 1609.344;

    private static readonly string[] _carParkTypes = ["parking", "parking_garage", "parking_lot"];

    // Fallback if Google rejects the newer parking sub-types.
    private static readonly string[] _carParkTypesSafe = ["parking"];

    private static readonly ConcurrentDictionary<string, CarParkSearchResult> _carParkCache = [];

    private static bool _carParkTypesRejected;

    /// <summary>
    /// Returns a plain-text list of candidate car parks near a point, nearest walk first, with Google
    /// place ids, addresses and walking distances. Fields are kept to the Nearby Search Pro SKU
    /// (no rating / userRatingCount, which would bill as Enterprise). Scriban: {{ cute.nearby_car_parks lat lon }}
    /// </summary>
    /// <param name="latitude">Latitude of the centre.</param>
    /// <param name="longitude">Longitude of the centre.</param>
    /// <param name="radiusMeters">Search radius (default and maximum 3219 m = 2 miles).</param>
    /// <param name="languageCode">Optional Google language code for names and addresses. Empty = Google default.</param>
    public static string NearbyCarParks(object latitude, object longitude, int radiusMeters = CarParkMaxRadiusMeters, string? languageCode = null)
    {
        var lat = ToCoordinate(latitude, nameof(latitude));
        var lon = ToCoordinate(longitude, nameof(longitude));

        var search = GetCarParkCandidates(lat, lon, radiusMeters, languageCode);

        return FormatCarParkBriefing(search);
    }

    [ScriptMemberIgnore]
    internal static CarParkSearchResult GetCarParkCandidates(double lat, double lon, int radiusMeters = CarParkMaxRadiusMeters, string? languageCode = null)
    {
        radiusMeters = Math.Clamp(radiusMeters, 1, CarParkMaxRadiusMeters);

        var cacheKey = string.Create(CultureInfo.InvariantCulture, $"{lat:F6}|{lon:F6}|{radiusMeters}|{languageCode}");

        if (_carParkCache.TryGetValue(cacheKey, out var cached)) return cached;

        var apiKey = RequireGoogleApiKey();

        var result = SearchCarParksAsync(apiKey, lat, lon, radiusMeters, languageCode).GetAwaiter().GetResult();

        _carParkCache.TryAdd(cacheKey, result);

        return result;
    }

    private static async Task<CarParkSearchResult> SearchCarParksAsync(string apiKey, double lat, double lon, int radiusMeters, string? languageCode)
    {
        List<CarParkCandidate> found;
        if (_carParkTypesRejected)
        {
            found = await SearchNearbyCarParks(apiKey, lat, lon, radiusMeters, _carParkTypesSafe, languageCode);
        }
        else
        {
            try
            {
                found = await SearchNearbyCarParks(apiKey, lat, lon, radiusMeters, _carParkTypes, languageCode);
            }
            catch (GoogleApiException ex) when (ex.StatusCode == 400)
            {
                // Remember for the rest of the run, so each centre isn't billed for two searches.
                _carParkTypesRejected = true;
                found = await SearchNearbyCarParks(apiKey, lat, lon, radiusMeters, _carParkTypesSafe, languageCode);
            }
        }

        foreach (var c in found)
        {
            c.StraightLineMeters = (int)Math.Round(Haversine.DistanceInKm(lon, c.Lon, lat, c.Lat) * 1000);
        }

        var candidates = FilterCarParkCandidates(found, radiusMeters);

        var walkingAvailable = await AddCarParkWalkingRoutes(apiKey, lat, lon, candidates);

        return new CarParkSearchResult(lat, lon, radiusMeters, walkingAvailable, candidates);
    }

    /// <summary>Operational only, inside the radius, one per place id, nearest N by straight line.</summary>
    internal static List<CarParkCandidate> FilterCarParkCandidates(IEnumerable<CarParkCandidate> found, int radiusMeters) =>
        found
            .Where(c => c.BusinessStatus is null || c.BusinessStatus == "OPERATIONAL")
            .Where(c => c.StraightLineMeters <= radiusMeters)
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .OrderBy(c => c.StraightLineMeters)
            .Take(CarParkCandidateCount)
            .ToList();

    private static async Task<List<CarParkCandidate>> SearchNearbyCarParks(string apiKey, double lat, double lon,
        int radiusMeters, string[] includedTypes, string? languageCode)
    {
        var body = new JObject
        {
            ["includedTypes"] = new JArray(includedTypes),
            ["maxResultCount"] = 20,
            ["rankPreference"] = "DISTANCE",
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
            "places.id,places.displayName,places.formattedAddress,places.location,places.primaryType,places.primaryTypeDisplayName,places.types,places.businessStatus");

        var places = JObject.Parse(json)["places"] as JArray ?? new JArray();

        return places
            .OfType<JObject>()
            .Where(p => p["id"]?.Value<string>() is not null && p.SelectToken("location.latitude") is not null)
            .Select(p => new CarParkCandidate
            {
                Id = p["id"]!.Value<string>()!,
                Name = p.SelectToken("displayName.text")?.Value<string>() ?? "(unnamed)",
                Address = p["formattedAddress"]?.Value<string>(),
                PrimaryType = p["primaryType"]?.Value<string>(),
                PrimaryTypeDisplay = p.SelectToken("primaryTypeDisplayName.text")?.Value<string>(),
                Types = p["types"]?.Values<string>().Where(t => t is not null).Cast<string>().ToList() ?? [],
                BusinessStatus = p["businessStatus"]?.Value<string>(),
                Lat = p.SelectToken("location.latitude")!.Value<double>(),
                Lon = p.SelectToken("location.longitude")?.Value<double>() ?? lon,
            })
            .ToList();
    }

    // Walking route from each car park (origin) to the centre (destination), in one matrix call.
    private static async Task<bool> AddCarParkWalkingRoutes(string apiKey, double lat, double lon, List<CarParkCandidate> candidates)
    {
        if (candidates.Count == 0) return true;

        var routes = await ComputeWalkingRoutesToCentre(apiKey, lat, lon, candidates.Select(c => (c.Lat, c.Lon)).ToList());

        if (routes is null) return false;

        for (var i = 0; i < candidates.Count && i < routes.Count; i++)
        {
            candidates[i].WalkingMeters = routes[i]?.Meters;
            candidates[i].WalkingMinutes = routes[i]?.Minutes;
        }

        return candidates.Any(c => c.WalkingMeters is not null);
    }

    // Routes API durations are strings like "345s".
    internal static int? ParseDurationMinutes(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration)) return null;

        var raw = duration.Trim().TrimEnd('s', 'S');

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? Math.Max(1, (int)Math.Round(seconds / 60))
            : null;
    }

    internal static string FormatCarParkBriefing(CarParkSearchResult search)
    {
        var sb = new StringBuilder();

        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Search: Google Places, parking within {search.RadiusMeters} m ({search.RadiusMeters / MetresPerMile:0.#} miles) straight-line of the centre."));

        sb.AppendLine(search.WalkingAvailable
            ? "Distances: Google walking route from the car park to the centre, straight-line in brackets."
            : "Distances: walking routes were NOT available - straight-line only.");

        sb.AppendLine();
        sb.AppendLine("Candidates (placeId | Google name | Google type | address | distance):");

        if (search.Candidates.Count == 0)
        {
            sb.AppendLine("- none within the radius");
            return sb.ToString().TrimEnd();
        }

        foreach (var c in OrderByDistance(search.Candidates))
        {
            var distance = c.WalkingMeters is not null
                ? string.Create(CultureInfo.InvariantCulture, $"walk {c.WalkingMeters} m{(c.WalkingMinutes is null ? "" : $", {c.WalkingMinutes} min")} ({c.StraightLineMeters} m)")
                : string.Create(CultureInfo.InvariantCulture, $"straight-line {c.StraightLineMeters} m");

            sb.Append("- ")
              .Append(c.Id).Append(" | ")
              .Append(c.Name).Append(" | ")
              .Append(c.PrimaryTypeDisplay ?? c.PrimaryType ?? "parking").Append(" | ")
              .Append(c.Address ?? "(no address)").Append(" | ")
              .Append(distance)
              .AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static IEnumerable<CarParkCandidate> OrderByDistance(IEnumerable<CarParkCandidate> candidates) =>
        candidates
            .OrderBy(c => c.WalkingMeters ?? (int)Math.Round(c.StraightLineMeters * 1.3))
            .ThenBy(c => c.StraightLineMeters);

    /// <summary>
    /// Builds the stored 'nearbyCarParks' value from the model's choice. Only place ids that Google
    /// returned are kept; every fact comes from Google; the list is re-sorted and capped here.
    /// Accepts {"carParks":[{"placeId","name"}]} or a bare array of the same objects.
    /// </summary>
    [ScriptMemberIgnore]
    public static JObject BuildNearbyCarParksField(JToken modelResult, double lat, double lon, DateTime? nowUtc = null)
    {
        var search = GetCarParkCandidates(lat, lon);

        return BuildNearbyCarParksField(modelResult, search, nowUtc ?? DateTime.UtcNow);
    }

    internal static JObject BuildNearbyCarParksField(JToken modelResult, CarParkSearchResult search, DateTime nowUtc)
    {
        var picks = modelResult switch
        {
            JObject o => o["carParks"] as JArray ?? new JArray(),
            JArray a => a,
            _ => new JArray(),
        };

        var byId = search.Candidates.ToDictionary(c => c.Id);

        var chosen = new List<(CarParkCandidate Candidate, string Name)>();

        foreach (var pick in picks.OfType<JObject>())
        {
            var placeId = pick["placeId"]?.Value<string>()?.Trim();

            if (placeId is null || !byId.TryGetValue(placeId, out var candidate)) continue;

            if (chosen.Any(c => c.Candidate.Id == placeId)) continue;

            var name = pick["name"]?.Value<string>()?.Trim();

            if (string.IsNullOrWhiteSpace(name) || name.Length > 100) name = candidate.Name;

            chosen.Add((candidate, name));
        }

        var carParks = new JArray(chosen
            .OrderBy(c => c.Candidate.WalkingMeters ?? (int)Math.Round(c.Candidate.StraightLineMeters * 1.3))
            .ThenBy(c => c.Candidate.StraightLineMeters)
            .Take(CarParkMaxResults)
            .Select(c =>
            {
                var distance = c.Candidate.WalkingMeters ?? c.Candidate.StraightLineMeters;

                return new JObject
                {
                    ["placeId"] = c.Candidate.Id,
                    ["name"] = c.Name,
                    ["address"] = c.Candidate.Address,
                    ["lat"] = Math.Round(c.Candidate.Lat, 6),
                    ["lng"] = Math.Round(c.Candidate.Lon, 6),
                    ["distanceMetres"] = distance,
                    ["distanceMiles"] = Math.Round(distance / MetresPerMile, 2),
                    ["distanceType"] = c.Candidate.WalkingMeters is null ? "straight_line" : "walking",
                    ["walkingMinutes"] = c.Candidate.WalkingMinutes,
                    ["straightLineMetres"] = c.Candidate.StraightLineMeters,
                };
            }));

        return new JObject
        {
            ["source"] = "google_places",
            ["generatedAt"] = nowUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["maxRadiusMiles"] = Math.Round(search.RadiusMeters / MetresPerMile, 1),
            ["carParks"] = carParks,
        };
    }

    internal sealed record CarParkSearchResult(double Lat, double Lon, int RadiusMeters, bool WalkingAvailable, IReadOnlyList<CarParkCandidate> Candidates);

    internal sealed class CarParkCandidate
    {
        public string Id { get; init; } = default!;
        public string Name { get; init; } = default!;
        public string? Address { get; init; }
        public string? PrimaryType { get; init; }
        public string? PrimaryTypeDisplay { get; init; }
        public List<string> Types { get; init; } = [];
        public string? BusinessStatus { get; init; }
        public double Lat { get; init; }
        public double Lon { get; init; }
        public int StraightLineMeters { get; set; }
        public int? WalkingMeters { get; set; }
        public int? WalkingMinutes { get; set; }
    }
}
