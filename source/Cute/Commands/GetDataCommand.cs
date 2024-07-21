using Contentful.Core.Errors;
using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Cute.Lib.GetDataAdapter;
using Cute.Lib.Serializers;
using Cute.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Concurrent;
using System.ComponentModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cute.Commands;

public class GetDataCommand : LoggedInCommand<GetDataCommand.Settings>
{
    public GetDataCommand(IConsoleWriter console, IPersistedTokenCache tokenCache)
        : base(console, tokenCache)
    { }

    public class Settings : CommandSettings
    {
        [CommandOption("-p|--path")]
        [Description("The local path to the YAML file(s) containg the get data definitions")]
        public string Path { get; set; } = @".\";

        [CommandOption("-s|--cache-seconds")]
        [Description("The local path to the YAML file(s) containg the get data definitions")]
        public int CacheSeconds { get; set; } = 0;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!Path.Exists(settings.Path))
        {
            return ValidationResult.Error($"Path not found '{settings.Path}'");
        }

        var files = Directory.GetFiles(settings.Path, "*.yaml");

        if (files.Length == 0)
        {
            return ValidationResult.Error($"No yaml definition files found in '{settings.Path}'");
        }

        if (settings.CacheSeconds < 0)
        {
            return ValidationResult.Error($"Invalid seconds setting for caches '{settings.CacheSeconds}'");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        if (result != 0 || _contentfulClient == null || _appSettings == null) return result;

        // Locales

        var locales = await _contentfulClient.GetLocalesCollection();

        var yaml = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var files = Directory.GetFiles(settings.Path, "*.yaml").OrderBy(f => f).ToArray();

        foreach (var file in files)
        {
            var getDataAdapter = yaml.Deserialize<GetDataAdapter>(System.IO.File.ReadAllText(file));

            var contentType = await _contentfulClient.GetContentType(getDataAdapter.ContentType);

            var serializer = new EntrySerializer(contentType, locales.Items);

            var dataResults = await GetDataViaHttp(getDataAdapter, settings.CacheSeconds == 0 ? null : _appSettings.TempFolder, settings.CacheSeconds);

            var ignoreFields = getDataAdapter.Mapping.Where(m => !m.Overwrite).Select(m => m.FieldName).ToHashSet();

            if (dataResults is null) return -1;

            _console.WriteNormal($"{dataResults.Count} new {contentType.SystemProperties.Id} entries...");

            _ = await CompareAndUpdateResults(
                dataResults, serializer,
                contentTypeId: getDataAdapter.ContentType,
                contentKeyField: getDataAdapter.ContentKeyField,
                contentDisplayField: getDataAdapter.ContentDisplayField,
                ignoreFields: ignoreFields
            );
        }

        return 0;
    }

    private readonly ConcurrentDictionary<string, Dictionary<string, Entry<JObject>>> _contentfulEntriesCache = [];

    private string ContentfulLookupList(string values, string contentType, string matchField, string returnField, string defaultValue)
    {
        if (string.IsNullOrEmpty(values))
        {
            values = defaultValue;
        }

        if (!_contentfulEntriesCache.TryGetValue(contentType, out var contentEntries))
        {
            contentEntries = ContentfulEntryEnumerator.Entries(_contentfulClient!, contentType, matchField)
                .ToBlockingEnumerable()
                .Where(e => e.Item1.Fields[matchField]?["en"] != null)
                .ToDictionary(e => e.Item1.Fields[matchField]?["en"]!.Value<string>()!, e => e.Item1, StringComparer.InvariantCultureIgnoreCase);

            _contentfulEntriesCache.TryAdd(contentType, contentEntries);
        }

        var lookupValues = values.Split(',').Select(s => s.Trim()).Select(s => contentEntries.ContainsKey(s) ? s : defaultValue);

        var resultValues = returnField.Equals("$id", StringComparison.OrdinalIgnoreCase)
            ? lookupValues.Select(s => contentEntries[s].SystemProperties.Id)
            : lookupValues.Select(s => contentEntries[s].Fields[returnField]?["en"]!.Value<string>());

        var retval = string.Join(',', resultValues.OrderBy(s => s));

        return retval;
    }

    private async Task<Dictionary<string, string>> CompareAndUpdateResults(List<Dictionary<string, string>> newRecords, EntrySerializer contentSerializer,
         string contentTypeId, string contentKeyField, string contentDisplayField, HashSet<string> ignoreFields)
    {
        if (_contentfulClient is null) return [];

        var entriesProcessed = new Dictionary<string, string>();

        await foreach (var (entry, entries) in ContentfulEntryEnumerator.Entries(_contentfulClient, contentTypeId, contentKeyField))
        {
            var cfEntry = contentSerializer.SerializeEntry(entry);

            if (cfEntry is null) continue;

            var key = cfEntry[contentKeyField]?.ToString();

            if (key is null) continue;

            entriesProcessed.Add(key, entry.SystemProperties.Id);

            var newRecord = newRecords.FirstOrDefault(c => c[contentKeyField] == key);

            if (newRecord is null) continue;

            _console.WriteNormal($"Contentful {contentTypeId} '{key}' matched with new entry '{newRecord[contentDisplayField]}'");

            var isChanged = false;
            Dictionary<string, (string?, string?)> changedFields = [];

            foreach (var (fieldName, value) in newRecord)
            {
                if (ignoreFields.Contains(fieldName)) continue;

                string? oldValue = null;

                if (cfEntry.TryGetValue(fieldName, out var oldValueObj))
                {
                    oldValue = cfEntry[fieldName]?.ToString();
                }

                var isFieldChanged = contentSerializer.CompareAndUpdateEntry(cfEntry, fieldName, value);

                if (isFieldChanged)
                {
                    changedFields.Add(fieldName, (oldValue, value));
                }

                isChanged = isFieldChanged || isChanged;
            }

            if (isChanged)
            {
                _console.WriteNormal($"Contentful {contentTypeId} '{key}' was updated by new entry '{newRecord[contentDisplayField]}'");
                foreach (var (fieldname, value) in changedFields)
                {
                    _console.WriteNormal($"...field '{fieldname}' changed from '{value.Item1}' to '{value.Item2}'");
                }
                await UpdateAndPublishEntry(contentSerializer.DeserializeEntry(cfEntry), contentTypeId);
            }
        }

        foreach (var newRecord in newRecords)
        {
            if (entriesProcessed.ContainsKey(newRecord[contentKeyField])) continue;

            var newContentfulRecord = contentSerializer.CreateNewFlatEntry();

            var newLanguageId = newContentfulRecord["sys.Id"]?.ToString() ?? "(error)";

            foreach (var (fieldName, value) in newRecord)
            {
                newContentfulRecord[fieldName] = value;
            }

            var newEntry = contentSerializer.DeserializeEntry(newContentfulRecord);

            _console.WriteNormal($"Creating {contentTypeId} '{newRecord[contentKeyField]}' - '{newRecord[contentDisplayField]}'");

            await UpdateAndPublishEntry(newEntry, contentTypeId);

            entriesProcessed.Add(newRecord[contentKeyField], newLanguageId);
        }

        return entriesProcessed;
    }

    private async Task UpdateAndPublishEntry(Entry<JObject> newEntry, string contentType)
    {
        _ = await _contentfulClient!.CreateOrUpdateEntry<JObject>(
                newEntry.Fields,
                id: newEntry.SystemProperties.Id,
                version: newEntry.SystemProperties.Version,
                contentTypeId: contentType);

        try
        {
            await _contentfulClient.PublishEntry(newEntry.SystemProperties.Id, newEntry.SystemProperties.Version!.Value + 1);
        }
        catch (ContentfulException ex)
        {
            _console.WriteAlert($"   --> Not published ({ex.Message})");
        }
    }

    private async Task<List<Dictionary<string, string>>?> GetDataViaHttp(GetDataAdapter adapter, string? outputPath = null, int cacheSeconds = 0)
    {
        var httpClient = new HttpClient()
        {
            BaseAddress = new Uri(adapter.EndPoint),
        };

        if (adapter.Headers is not null)
        {
            foreach (var (key, value) in adapter.Headers)
            {
                httpClient.DefaultRequestHeaders.Add(key, value);
            }
        }

        FormUrlEncodedContent? formContent = null;

        if (adapter.FormUrlEncodedContent is not null)
        {
            formContent = new FormUrlEncodedContent(adapter.FormUrlEncodedContent);
        }

        var returnValue = new List<Dictionary<string, string>>();
        var cachedResults = new HashSet<string>();

        if (outputPath is not null && cacheSeconds > 0)
        {
            var count = 1;
            while (true)
            {
                var filename = System.IO.Path.Combine(outputPath, $"{adapter.ContentType}-{httpClient.BaseAddress.Host.Replace('.', '_')}-{count:D4}.json");

                if (!System.IO.File.Exists(filename)) break;

                if (DateTime.UtcNow.AddSeconds(-cacheSeconds) > System.IO.File.GetLastWriteTimeUtc(filename)) break;

                cachedResults.Add(filename);

                count++;
            }
        }

        var requestCount = 1;

        while (true)
        {
            HttpResponseMessage? endpointResult = null;

            string? endpointContent = null;

            if (outputPath is not null && cachedResults.Count != 0)
            {
                var filename = System.IO.Path.Combine(outputPath, $"{adapter.ContentType}-{httpClient.BaseAddress.Host.Replace('.', '_')}-{requestCount:D4}.json");

                if (!cachedResults.Contains(filename)) break;

                _console.WriteNormal($"...reading from cache {filename}...");

                endpointContent = await System.IO.File.ReadAllTextAsync(filename);
            }
            else
            {
                if (adapter.HttpMethod == Lib.GetDataAdapter.HttpMethod.Post)
                {
                    endpointResult = await httpClient.PostAsync("", formContent);
                }
                else if (adapter.HttpMethod == Lib.GetDataAdapter.HttpMethod.Get)
                {
                    endpointResult = await httpClient.GetAsync("");
                }

                if (endpointResult is not null)
                {
                    endpointContent = await endpointResult.Content.ReadAsStringAsync();
                }

                if (outputPath is not null && cacheSeconds > 0)
                {
                    Directory.CreateDirectory(outputPath);

                    var filename = System.IO.Path.Combine(outputPath, $"{adapter.ContentType}-{httpClient.BaseAddress.Host.Replace('.', '_')}-{requestCount:D4}.json");

                    _console.WriteNormal($"...writing to cache {filename}...");

                    System.IO.File.WriteAllText(filename, endpointContent, System.Text.Encoding.UTF8);
                }
            }

            if (endpointContent is null) return [];

            var results = JsonConvert.DeserializeObject(endpointContent);

            if (results is null) return null;

            JArray rootArray = [];

            if (adapter.QueryResultsKey is null)
            {
                if (results is JArray jArr)
                {
                    rootArray = jArr;
                }
                else
                {
                    return [];
                }
            }
            else if (results is JToken obj)
            {
                foreach (var key in adapter.QueryResultsKey)
                {
                    var node = obj[key];
                    if (node is JToken jNode)
                    {
                        obj = jNode;
                    }
                    else
                    {
                        return [];
                    }
                }
                if (obj is JArray jArr)
                {
                    rootArray = jArr;
                }
                else
                {
                    return [];
                }
            }
            else
            {
                return [];
            }

            _console.WriteNormal($"...{httpClient.BaseAddress.Host} returned {rootArray.Count} entries...");

            var converter = new Html2Markdown.Converter();

            var scriptObjectGlobal = new ScriptObject();

            scriptObjectGlobal.Import("cf_lookup_list", new Func<string, string, string, string, string, string>(ContentfulLookupList));

            scriptObjectGlobal.Import("cf_html_to_markdown", new Func<string, string>(input => input is null ? string.Empty : converter.Convert(input)));

            var templates = adapter.Mapping.ToDictionary(m => m.FieldName, m => Template.Parse(m.Expression));

            var templateContext = new TemplateContext();

            templateContext.PushGlobal(scriptObjectGlobal);

            var batchValue = rootArray.Cast<JObject>()
                .Select(o =>
                {
                    var scriptObjectInstance = new ScriptObject();
                    scriptObjectInstance.Import(new { row = o });
                    templateContext.PushGlobal(scriptObjectInstance);
                    var newRecord = templates.ToDictionary(t => t.Key, t => t.Value.Render(templateContext));
                    templateContext.PopGlobal();
                    return newRecord;
                })
                .ToList();

            returnValue.AddRange(batchValue);

            if (adapter.ContinuationTokenHeader is null) break;

            if (cachedResults.Count > 0)
            {
                requestCount++;
                continue;
            }

            if (endpointResult is null) break;

            if (!endpointResult.Headers.Contains(adapter.ContinuationTokenHeader))
            {
                break;
            }

            if (httpClient.DefaultRequestHeaders.Contains(adapter.ContinuationTokenHeader))
            {
                httpClient.DefaultRequestHeaders.Remove(adapter.ContinuationTokenHeader);
            }

            var token = endpointResult.Headers.GetValues(adapter.ContinuationTokenHeader).First();

            httpClient.DefaultRequestHeaders.Add(adapter.ContinuationTokenHeader, token);

            requestCount++;
        }

        return [.. returnValue.OrderBy(e => e[adapter.ContentKeyField])];
    }
}