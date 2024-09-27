using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Cute.Lib.Utilities;
using Html2Markdown;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scriban.Runtime;
using System.Collections.Concurrent;

namespace Cute.Lib.Scriban;

public class CuteFunctions : ScriptObject
{
    [ScriptMemberIgnore]
    public static ContentfulConnection ContentfulConnection { get; set; } = default!;

    private static readonly Converter _htmlConverter = new();

    public static string HtmlToMarkdown(string? content)
    {
        if (content == null) return string.Empty;

        return _htmlConverter.Convert(content);
    }

    public static string ToJson(object value, params string[]? keepList)
    {
        if (value is JToken jToken)
        {
            return keepList is null
                ? jToken.ToString()
                : SelectProperties(jToken, keepList)?.ToString() ?? string.Empty;
        }
        return JsonConvert.SerializeObject(value);
    }

    private static JToken? SelectProperties(JToken jToken, params string[] keepList)
    {
        var jOldObjectList = new List<JObject>();

        var jNewObjectList = new List<JObject>();

        if (jToken is JArray jArray)
        {
            foreach (var obj in jArray.Cast<JObject>())
            {
                jOldObjectList.Add(obj);
            }
        }
        else if (jToken is JObject jObject)
        {
            jOldObjectList.Add(jObject);
        }

        foreach (var obj in jOldObjectList)
        {
            var newObj = new JObject();
            foreach (var propName in keepList)
            {
                newObj.Add(propName, obj[propName]);
            }
            jNewObjectList.Add(newObj);
        }

        JToken? returnValue = null;

        if (jToken is JObject)
        {
            returnValue = jNewObjectList[0];
        }
        else if (jToken is JArray)
        {
            var returnArray = new JArray();
            foreach (var obj in jNewObjectList) returnArray.Add(obj);
            returnValue = returnArray;
        }

        return returnValue;
    }

    public static string UrlLastSegment(string? url)
    {
        if (url == null) return string.Empty;

        if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute)) return string.Empty;

        var uri = new Uri(url);

        return uri.Segments.LastOrDefault() ?? string.Empty;
    }

    public static object? Switch(object value, params object[] values)
    {
        if (values.Length < 3) return null;

        if (values.Length % 2 == 0) return null;

        if (value == null) return values[^1];

        for (var i = 0; i + 1 < values.Length; i += 2)
        {
            if (value.ToString() == values[i]?.ToString())
            {
                return values[i + 1];
            }
        }

        return values[^1];
    }

    public static bool EntriesExist(string contentType, string fieldName, string id)
    {
        var cacheKey = $"{contentType}|{fieldName}";

        var countCache = GetFromCountCache(contentType, fieldName, cacheKey);

        if (countCache.TryGetValue(id, out var count))
        {
            return count > 0;
        }

        return false;
    }

    private static readonly ConcurrentDictionary<string, Dictionary<string, int>> _countEntriesCache = [];

    public static int EntriesCount(string contentType, string fieldName, string id)
    {
        var cacheKey = $"{contentType}|{fieldName}";

        var countCache = GetFromCountCache(contentType, fieldName, cacheKey);

        if (countCache.TryGetValue(id, out var count))
        {
            return count;
        }

        return 0;
    }

    private static Dictionary<string, int> GetFromCountCache(string contentType, string keyField, string cacheKey)
    {
        if (!_countEntriesCache.TryGetValue(cacheKey, out var countResult))
        {
            lock (_countEntriesCache)
            {
                if (!_countEntriesCache.TryGetValue(cacheKey, out var _))
                {
                    var results = ContentfulConnection.GetManagementEntries<JObject>(
                            new EntryQuery.Builder()
                                .WithContentType(contentType)
                                .WithIncludeLevels(1)
                                .Build()
                        )
                        .ToBlockingEnumerable();

                    var resultsDictionary = new Dictionary<string, int>();

                    foreach (var (entry, _) in results)
                    {
                        var key = entry.SelectToken(keyField)?.Value<string>() ?? "error";
                        if (resultsDictionary.TryGetValue(key, out var count))
                        {
                            resultsDictionary[key] = count + 1;
                        }
                        else
                        {
                            resultsDictionary.Add(key, 1);
                        }
                    }

                    _countEntriesCache.TryAdd(cacheKey, resultsDictionary);
                }
            }
            countResult = _countEntriesCache[cacheKey];
        }

        return countResult;
    }

    private static readonly ConcurrentDictionary<string, List<Location>> _nearEntriesCache = [];

    public static bool Near(string contentType, string matchField, double radiusInKm, double lat, double lon)
    {
        var cacheKey = $"{contentType}|{matchField}";

        var contentEntries = GetFromNearCache(contentType, matchField, cacheKey);

        var boundingBox = Haversine.GetBoundingBox(lon, lat, radiusInKm);

        return contentEntries
            .Where(l => boundingBox.Contains(l.Lon, l.Lat))
            .Any();
    }

    private static List<Location> GetFromNearCache(string contentType, string locationField, string cacheKey)
    {
        if (!_nearEntriesCache.TryGetValue(cacheKey, out var contentEntries))
        {
            lock (_nearEntriesCache)
            {
                var contentEntriesLoader = ContentfulConnection.GetManagementEntries<Entry<JObject>>(
                        new EntryQuery.Builder()
                            .WithContentType(contentType)
                            .WithOrderByField(locationField)
                            .Build()
                    )
                    .ToBlockingEnumerable()
                    .Where(e => e.Entry.Fields[locationField]?["en"] != null)
                    .Select(e => e.Entry.Fields[locationField]?["en"]!.ToObject<Location>()!)
                    .ToList();

                _nearEntriesCache.TryAdd(cacheKey, contentEntriesLoader);
            }
            contentEntries = _nearEntriesCache[cacheKey];
        }

        return contentEntries;
    }

    private static readonly ConcurrentDictionary<string, Dictionary<string, Entry<JObject>>> _lookupEntriesCache = [];

    public static string LookupList(string values, string contentType, string matchField, string returnField, string defaultValue)
    {
        var cacheKey = $"{contentType}|{matchField}";

        if (string.IsNullOrEmpty(values))
        {
            values = defaultValue;
        }

        var contentEntries = GetFromLookupCache(contentType, matchField, cacheKey);

        var lookupValues = values.Split(',').Select(s => s.Trim()).Select(s => contentEntries.ContainsKey(s) ? s : defaultValue);

        var resultValues = returnField.Equals("$id", StringComparison.OrdinalIgnoreCase)
            ? lookupValues.Select(s => contentEntries[s].SystemProperties.Id)
            : lookupValues.Select(s => contentEntries[s].Fields[returnField]?["en"]!.Value<string>());

        var retval = string.Join(',', resultValues.OrderBy(s => s));

        return retval;
    }

    private static Dictionary<string, Entry<JObject>> GetFromLookupCache(string contentType, string matchField, string cacheKey)
    {
        if (!_lookupEntriesCache.TryGetValue(cacheKey, out var contentEntries))
        {
            lock (_lookupEntriesCache)
            {
                var contentEntriesLoader = ContentfulConnection.GetManagementEntries<Entry<JObject>>(
                        new EntryQuery.Builder()
                            .WithContentType(contentType)
                            .WithOrderByField(matchField)
                            .Build()
                    )
                    .ToBlockingEnumerable()
                    .Where(e => e.Entry.Fields[matchField]?["en"] != null)
                    .ToDictionary(e => e.Entry.Fields[matchField]?["en"]!.Value<string>()!, e => e.Entry, StringComparer.InvariantCultureIgnoreCase);

                _lookupEntriesCache.TryAdd(cacheKey, contentEntriesLoader);
            }
            contentEntries = _lookupEntriesCache[cacheKey];
        }

        return contentEntries;
    }

    public static int ToInt(string value)
    {
        return Convert.ToInt32(value);
    }
}