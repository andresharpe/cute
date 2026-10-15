using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Cute.Lib.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scriban.Runtime;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Cute.Lib.Scriban;

/// <summary>
/// Google Maps Platform helpers for prompts. Used by the location naming job
/// (cuteContentGenerate 'DataLocation.AllLocations.UniqueName') to give the model
/// verified landmarks and walking distances instead of letting it guess.
///
/// Requires 'Cute__GoogleApiKey' with Places API (New) and Routes API enabled.
/// Geocoding API is optional (used for the district name only).
/// </summary>
public partial class CuteFunctions
{
    [ScriptMemberIgnore]
    public static string? GoogleApiKey { get; set; }

    [ScriptMemberIgnore]
    public static HttpClient GoogleHttpClient { get; set; } = new() { Timeout = TimeSpan.FromSeconds(30) };

    // Places Google drops as a single pin in the middle of a large area.
    // Distances to these are to the pin, so the edge may be closer.
    private static readonly HashSet<string> _areaPlaceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "park", "national_park", "state_park", "university", "stadium", "campground",
        "botanical_garden", "zoo", "amusement_park", "golf_course", "hiking_area", "garden",
    };

    private static readonly string[] _landmarkTypes =
    [
        "tourist_attraction", "museum", "park", "train_station", "subway_station", "stadium",
        "performing_arts_theater", "convention_center", "university", "historical_landmark",
        "shopping_mall", "plaza",
    ];

    // Fallback if Google rejects one of the types above (the type list changes over time).
    private static readonly string[] _landmarkTypesSafe =
    [
        "tourist_attraction", "museum", "park", "train_station", "subway_station", "stadium",
        "performing_arts_theater", "convention_center", "university", "shopping_mall",
    ];

    private static readonly string[] _transitTypes =
    [
        "train_station", "subway_station", "light_rail_station", "ferry_terminal",
    ];

    // Too granular to name a centre after (and Google returns many of them).
    private static readonly HashSet<string> _minorStopTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bus_stop", "bus_station", "tram_stop", "transit_stop", "transit_station", "taxi_stand", "parking",
    };

    // Below this many Google reviews a landmark is not "a place people already know".
    private const int MinLandmarkReviews = 50;

    private static readonly ConcurrentDictionary<string, string> _nearbyPlacesCache = [];

    /// <summary>
    /// Returns a plain-text briefing of well-known places around a point, with Google walking
    /// distances. Scriban: {{ cute.nearby_places lat lon 500 }}
    /// </summary>
    /// <param name="latitude">Latitude of the centre.</param>
    /// <param name="longitude">Longitude of the centre.</param>
    /// <param name="radiusMeters">Search radius for landmarks (default 500).</param>
    /// <param name="transitRadiusMeters">Search radius for the nearest stations (default 1500).</param>
    /// <param name="languageCode">Optional Google language code for place names (e.g. "nl"). Empty = Google default.</param>
    public static string NearbyPlaces(object latitude, object longitude, int radiusMeters = 500, int transitRadiusMeters = 1500, string? languageCode = null)
    {
        var lat = ToCoordinate(latitude, nameof(latitude));
        var lon = ToCoordinate(longitude, nameof(longitude));

        var cacheKey = string.Create(CultureInfo.InvariantCulture, $"{lat:F6}|{lon:F6}|{radiusMeters}|{transitRadiusMeters}|{languageCode}");

        if (_nearbyPlacesCache.TryGetValue(cacheKey, out var cached)) return cached;

        var apiKey = GoogleApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Google API key not configured. Set 'Cute__GoogleApiKey' (Places API (New) and Routes API enabled).");
        }

        var result = NearbyPlacesAsync(apiKey, lat, lon, radiusMeters, transitRadiusMeters, languageCode)
            .GetAwaiter().GetResult();

        _nearbyPlacesCache.TryAdd(cacheKey, result);

        return result;
    }

    private static async Task<string> NearbyPlacesAsync(string apiKey, double lat, double lon,
        int radiusMeters, int transitRadiusMeters, string? languageCode)
    {
        // 1. Landmarks by popularity inside the walking radius.
        List<PlaceCandidate> landmarks;
        try
        {
            landmarks = await SearchNearby(apiKey, lat, lon, radiusMeters, _landmarkTypes, "POPULARITY", 20, languageCode);
        }
        catch (GoogleApiException ex) when (ex.StatusCode == 400)
        {
            landmarks = await SearchNearby(apiKey, lat, lon, radiusMeters, _landmarkTypesSafe, "POPULARITY", 20, languageCode);
        }

        // 2. Nearest stations (wider radius so we always know the nearest one, even if beyond walking range).
        var transit = await SearchNearby(apiKey, lat, lon, transitRadiusMeters, _transitTypes, "DISTANCE", 6, languageCode);

        var candidates = landmarks
            .Concat(transit)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .Where(p => !_minorStopTypes.Contains(p.PrimaryType ?? string.Empty))
            .Where(p => IsStation(p) || (p.UserRatingCount ?? 0) >= MinLandmarkReviews)
            .ToList();

        // 3. Walking distances in one matrix call (falls back to straight-line on failure).
        var walkingAvailable = await AddWalkingDistances(apiKey, lat, lon, candidates);

        foreach (var c in candidates)
        {
            c.StraightLineMeters = (int)Math.Round(Haversine.DistanceInKm(lon, c.Lon, lat, c.Lat) * 1000);
        }

        // Same name returned several times (e.g. one pin per platform) - keep the nearest.
        candidates = candidates
            .GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(c => c.WalkingMeters ?? int.MaxValue).ThenBy(c => c.StraightLineMeters).First())
            .ToList();

        // 4. District / neighbourhood names (optional - Geocoding API).
        var districts = await ReverseGeocodeDistricts(apiKey, lat, lon, languageCode);

        var sb = new StringBuilder();

        sb.AppendLine(districts.Count > 0
            ? $"District / neighbourhood (Google reverse geocode, most specific first): {string.Join(" > ", districts)}"
            : "District / neighbourhood: not available from Google - use the address.");

        sb.AppendLine(walkingAvailable
            ? "Distances are Google walking routes from the centre."
            : "Walking routes were NOT available - distances are straight-line; real walking distance is roughly 1.3x longer.");

        sb.AppendLine();
        sb.AppendLine("Candidate places (name | type | Google reviews | distance | notes):");

        if (candidates.Count == 0)
        {
            sb.AppendLine("- none found");
        }

        foreach (var c in candidates.OrderBy(c => c.WalkingMeters ?? (int)(c.StraightLineMeters * 1.3)))
        {
            var distance = c.WalkingMeters is not null
                ? $"walk {c.WalkingMeters} m"
                : $"straight-line {c.StraightLineMeters} m";

            var notes = new List<string>();

            if (c.Types.Any(_areaPlaceTypes.Contains) || _areaPlaceTypes.Contains(c.PrimaryType ?? string.Empty))
            {
                notes.Add("large area - distance is to a pin in the middle, the edge is closer");
            }
            if (IsStation(c))
            {
                notes.Add("station");
            }

            sb.Append("- ")
              .Append(c.Name)
              .Append(" | ")
              .Append(c.PrimaryTypeDisplay ?? c.PrimaryType ?? "place")
              .Append(" | ")
              .Append(c.UserRatingCount?.ToString("N0", CultureInfo.InvariantCulture) ?? "0")
              .Append(" | ")
              .Append(distance);

            if (notes.Count > 0) sb.Append(" | ").Append(string.Join("; ", notes));

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static async Task<List<PlaceCandidate>> SearchNearby(string apiKey, double lat, double lon,
        int radiusMeters, string[] includedTypes, string rankPreference, int maxResults, string? languageCode)
    {
        var body = new JObject
        {
            ["includedTypes"] = new JArray(includedTypes),
            ["maxResultCount"] = Math.Clamp(maxResults, 1, 20),
            ["rankPreference"] = rankPreference,
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
            "places.id,places.displayName,places.primaryType,places.primaryTypeDisplayName,places.types,places.userRatingCount,places.location");

        var places = JObject.Parse(json)["places"] as JArray ?? new JArray();

        return places
            .OfType<JObject>()
            .Select(p => new PlaceCandidate
            {
                Id = p["id"]?.Value<string>() ?? Guid.NewGuid().ToString(),
                Name = p.SelectToken("displayName.text")?.Value<string>() ?? "(unnamed)",
                PrimaryType = p["primaryType"]?.Value<string>(),
                PrimaryTypeDisplay = p.SelectToken("primaryTypeDisplayName.text")?.Value<string>(),
                Types = p["types"]?.Values<string>().Where(t => t is not null).Cast<string>().ToList() ?? [],
                UserRatingCount = p["userRatingCount"]?.Value<int?>(),
                Lat = p.SelectToken("location.latitude")?.Value<double>() ?? lat,
                Lon = p.SelectToken("location.longitude")?.Value<double>() ?? lon,
            })
            .ToList();
    }

    private static async Task<bool> AddWalkingDistances(string apiKey, double lat, double lon, List<PlaceCandidate> candidates)
    {
        if (candidates.Count == 0) return true;

        var body = new JObject
        {
            ["origins"] = new JArray(new JObject
            {
                ["waypoint"] = new JObject
                {
                    ["location"] = new JObject
                    {
                        ["latLng"] = new JObject { ["latitude"] = lat, ["longitude"] = lon },
                    },
                },
            }),
            ["destinations"] = new JArray(candidates.Select(c => new JObject
            {
                ["waypoint"] = new JObject
                {
                    ["location"] = new JObject
                    {
                        ["latLng"] = new JObject { ["latitude"] = c.Lat, ["longitude"] = c.Lon },
                    },
                },
            })),
            ["travelMode"] = "WALK",
        };

        try
        {
            var json = await PostGoogle(apiKey,
                "https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix",
                body.ToString(Formatting.None),
                "originIndex,destinationIndex,distanceMeters,condition");

            var token = JToken.Parse(json);

            var elements = token as JArray ?? (token["elements"] as JArray) ?? new JArray();

            foreach (var element in elements.OfType<JObject>())
            {
                var index = element["destinationIndex"]?.Value<int?>() ?? -1;

                if (index < 0 || index >= candidates.Count) continue;

                if (element["condition"]?.Value<string>() is string condition && condition != "ROUTE_EXISTS") continue;

                candidates[index].WalkingMeters = element["distanceMeters"]?.Value<int?>();
            }

            return candidates.Any(c => c.WalkingMeters is not null);
        }
        catch (GoogleApiException)
        {
            return false;
        }
    }

    private static async Task<List<string>> ReverseGeocodeDistricts(string apiKey, double lat, double lon, string? languageCode)
    {
        try
        {
            var url = string.Create(CultureInfo.InvariantCulture,
                $"https://maps.googleapis.com/maps/api/geocode/json?latlng={lat},{lon}&result_type=neighborhood|sublocality&key={Uri.EscapeDataString(apiKey)}");

            if (!string.IsNullOrWhiteSpace(languageCode))
            {
                url += $"&language={Uri.EscapeDataString(languageCode)}";
            }

            using var response = await GoogleHttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) return [];

            var root = JObject.Parse(await response.Content.ReadAsStringAsync());

            if (root["status"]?.Value<string>() != "OK") return [];

            var order = new[] { "neighborhood", "sublocality_level_2", "sublocality_level_1", "sublocality" };

            var names = new List<string>();

            var components = (root["results"] as JArray ?? new JArray())
                .SelectMany(r => r["address_components"] as JArray ?? new JArray())
                .OfType<JObject>()
                .ToList();

            foreach (var type in order)
            {
                foreach (var component in components.Where(c => (c["types"] as JArray)?.Values<string>().Contains(type) == true))
                {
                    var name = component["long_name"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        names.Add(name);
                    }
                }
            }

            return names;
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static async Task<string> PostGoogle(string apiKey, string url, string body, string fieldMask)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-Goog-Api-Key", apiKey);
            request.Headers.Add("X-Goog-FieldMask", fieldMask);

            using var response = await GoogleHttpClient.SendAsync(request);

            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode) return content;

            var status = (int)response.StatusCode;

            // Retry throttling and transient server errors only.
            if ((status == 429 || status >= 500) && attempt < 4)
            {
                await Task.Delay(1000 * attempt);
                continue;
            }

            var message = JObject.Parse(content.StartsWith('{') ? content : "{}").SelectToken("error.message")?.Value<string>()
                ?? response.ReasonPhrase;

            throw new GoogleApiException(status, $"Google API call to '{new Uri(url).AbsolutePath}' failed ({status}): {message}");
        }
    }

    /// <summary>
    /// Lists the uniqueName values already used by other centres near a point, so a new name
    /// can be kept unique. Reads preview (draft) content, so names written earlier in the same
    /// run are included. Scriban: {{ cute.nearby_unique_names lat lon 25 dataLocation.sys.id }}
    /// </summary>
    public static string NearbyUniqueNames(object latitude, object longitude, double radiusKm = 25, string? excludeEntryId = null)
    {
        var lat = ToCoordinate(latitude, nameof(latitude));
        var lon = ToCoordinate(longitude, nameof(longitude));

        var locale = ContentfulConnection.GetDefaultLocaleAsync().GetAwaiter().GetResult().Code;

        // Management API: supports the [within] location filter (GraphQL delivery does not) and
        // returns the latest draft values, so names written earlier in the same run are seen.
        var entryQuery = new EntryQuery.Builder()
            .WithContentType("dataLocation")
            .WithIncludeLevels(0)
            .WithQueryString(string.Create(CultureInfo.InvariantCulture,
                $"fields.latLon[within]={lat},{lon},{radiusKm}&fields.uniqueName[exists]=true&select=sys,fields.name,fields.uniqueName"))
            .Build();

        string? FieldValue(Entry<JObject> entry, string field) =>
            entry.Fields?[field]?[locale]?.Value<string>();

        var items = ContentfulConnection.GetManagementEntries<Entry<JObject>>(entryQuery)
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .Where(e => e.SystemProperties.Id != excludeEntryId)
            .Select(e => new
            {
                UniqueName = FieldValue(e, "uniqueName")?.Trim(),
                Name = FieldValue(e, "name"),
            })
            .Where(i => !string.IsNullOrWhiteSpace(i.UniqueName) && !IsClosedCentre(i.Name) && !IsClosedCentre(i.UniqueName))
            .OrderBy(i => i.UniqueName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (items.Count == 0) return "- none";

        return string.Join('\n', items.Select(i => $"- {i.UniqueName} (centre: {i.Name})"));
    }

    // Closed centres free their name; they are marked 'zz...' and/or '(Closed)' in Contentful.
    internal static bool IsClosedCentre(string? value) =>
        value is not null
        && (value.TrimStart().StartsWith("zz", StringComparison.OrdinalIgnoreCase)
            || value.Contains("closed", StringComparison.OrdinalIgnoreCase));

    private static bool IsStation(PlaceCandidate place) =>
        _transitTypes.Contains(place.PrimaryType ?? string.Empty)
        || (!_minorStopTypes.Contains(place.PrimaryType ?? string.Empty) && place.Types.Any(t => _transitTypes.Contains(t)));

    // Template values arrive as JValue/string/double depending on the source - normalise them.
    private static double ToCoordinate(object? value, string name)
    {
        var raw = value is JValue jValue ? jValue.Value : value;

        return raw switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new ArgumentException($"'{name}' must be a number (got '{value ?? "null"}'). Does the query return latLon {{ lat lon }}?"),
        };
    }

    private sealed class PlaceCandidate
    {
        public string Id { get; init; } = default!;
        public string Name { get; init; } = default!;
        public string? PrimaryType { get; init; }
        public string? PrimaryTypeDisplay { get; init; }
        public List<string> Types { get; init; } = [];
        public int? UserRatingCount { get; init; }
        public double Lat { get; init; }
        public double Lon { get; init; }
        public int? WalkingMeters { get; set; }
        public int StraightLineMeters { get; set; }
    }

    private sealed class GoogleApiException(int statusCode, string message) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;
    }
}
