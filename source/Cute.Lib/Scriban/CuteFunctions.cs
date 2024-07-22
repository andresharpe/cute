using Contentful.Core;
using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Html2Markdown;
using Newtonsoft.Json.Linq;
using Scriban.Runtime;
using System.Collections.Concurrent;

namespace Cute.Lib.Scriban;

public class CuteFunctions : ScriptObject
{
    [ScriptMemberIgnore]
    public static ContentfulManagementClient ContentfulManagementClient { get; set; } = default!;

    private static readonly Converter _htmlConverter = new();

    public static string HtmlToMarkdown(string? content)
    {
        if (content == null) return string.Empty;

        return _htmlConverter.Convert(content);
    }

    private static readonly ConcurrentDictionary<string, Dictionary<string, Entry<JObject>>> _contentfulEntriesCache = [];

    public static string LookupList(string values, string contentType, string matchField, string returnField, string defaultValue)
    {
        var cacheKey = $"{contentType}|{matchField}";

        if (string.IsNullOrEmpty(values))
        {
            values = defaultValue;
        }

        if (!_contentfulEntriesCache.TryGetValue(cacheKey, out var contentEntries))
        {
            contentEntries = ContentfulEntryEnumerator.Entries(ContentfulManagementClient, contentType, matchField)
                .ToBlockingEnumerable()
                .Where(e => e.Item1.Fields[matchField]?["en"] != null)
                .ToDictionary(e => e.Item1.Fields[matchField]?["en"]!.Value<string>()!, e => e.Item1, StringComparer.InvariantCultureIgnoreCase);

            _contentfulEntriesCache.TryAdd(cacheKey, contentEntries);
        }

        var lookupValues = values.Split(',').Select(s => s.Trim()).Select(s => contentEntries.ContainsKey(s) ? s : defaultValue);

        var resultValues = returnField.Equals("$id", StringComparison.OrdinalIgnoreCase)
            ? lookupValues.Select(s => contentEntries[s].SystemProperties.Id)
            : lookupValues.Select(s => contentEntries[s].Fields[returnField]?["en"]!.Value<string>());

        var retval = string.Join(',', resultValues.OrderBy(s => s));

        return retval;
    }
}