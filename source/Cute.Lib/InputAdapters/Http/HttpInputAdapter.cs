using Contentful.Core.Models;
using Cute.Lib.Cache;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.InputAdapters.Http.Models;
using Cute.Lib.InputAdapters.MemoryAdapters;
using Cute.Lib.Scriban;
using Cute.Lib.Serializers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using System.IO.Hashing;
using System.Text;

namespace Cute.Lib.InputAdapters.Http;

public class HttpInputAdapter(
    HttpDataAdapterConfig adapter,
    ContentfulConnection contentfulConnection,
    ContentLocales contentLocales,
    IReadOnlyDictionary<string, string?> envSettings,
    IEnumerable<ContentType> contentTypes,
    HttpClient httpClient)
    : InputAdapterBase(adapter.EndPoint)
{
    private readonly HttpDataAdapterConfig _adapter = adapter;

    private readonly ContentLocales _contentLocales = contentLocales;

    private readonly HttpClient _httpClient = httpClient;

    private readonly ScriptObject _scriptObject = CreateScriptObject(contentfulConnection, envSettings);

    private readonly Dictionary<Template, Template> _compiledTemplates = adapter.CompileMappingTemplates();

    private readonly Dictionary<string, Template> _compiledPreTemplates = adapter.CompilePreMappingTemplates();

    private readonly ContentEntryEnumerators? _entryEnumerators = GetEntryEnumerators(adapter.EnumerateForContentTypes, contentfulConnection, contentTypes);

    private HttpResponseFileCache? _httpResponseFileCache;

    public HttpInputAdapter WithHttpResponseFileCache(HttpResponseFileCache? httpResponseFileCache)
    {
        _httpResponseFileCache = httpResponseFileCache;

        return this;
    }

    private ContentType _contentType = default!;

    private List<Dictionary<string, string>> _results = default!;

    private EntrySerializer _serializer = default!;

    private int _currentRecordIndex = -1;

    public override async Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        if (_currentRecordIndex == -1)
        {
            await GetRecordCountAsync();
        }

        if (_currentRecordIndex >= _results.Count)
        {
            return null;
        }

        var result = _serializer.CreateNewFlatEntry(_results[_currentRecordIndex]);

        _currentRecordIndex++;

        return result;
    }

    public override async Task<int> GetRecordCountAsync()
    {
        if (_results is not null && _results.Count > 0) return _results.Count;

        _contentType = contentTypes.FirstOrDefault(ct => ct.SystemProperties.Id == _adapter.ContentType)
            ?? throw new CliException($"Content type '{_adapter.ContentType}' does not exist.");

        _serializer = new EntrySerializer(_contentType, _contentLocales);

        if (_entryEnumerators is null)
        {
            FormUrlEncodedContent? formContent = null;
            if (_adapter.FormUrlEncodedContent is not null)
            {
                formContent = new FormUrlEncodedContent(_adapter.FormUrlEncodedContent);
            }
            _results = await MakeHttpCall(formContent);
        }
        else
        {
            _results = await MakeHttpCallsForEnumerators();
        }

        _currentRecordIndex = 0;

        return _results.Count;
    }

    private async Task<List<Dictionary<string, string>>> MakeHttpCallsForEnumerators(int level = 0, List<Dictionary<string, string>> returnVal = null!)
    {
        if (_entryEnumerators is null) throw new CliException("No entry enumerators defined.");

        if (level > _entryEnumerators.Length - 1)
        {
            FormUrlEncodedContent? formContent = null;

            if (_adapter.FormUrlEncodedContent is not null)
            {
                var compiledFormUrlEncodedContent = _adapter.FormUrlEncodedContent.
                    ToDictionary(kv => kv.Key, kv => Template.Parse(kv.Value).Render(_scriptObject));

                formContent = new FormUrlEncodedContent(compiledFormUrlEncodedContent);
            }

            returnVal.AddRange(await MakeHttpCall(formContent) ?? []);

            return returnVal;
        }

        returnVal ??= [];

        Template? filterTemplate = null;

        if (_adapter.EnumerateForContentTypes[level].Filter is not null)
        {
            filterTemplate = Template.Parse(_adapter.EnumerateForContentTypes[level].Filter);
        }

        var padding = new string(' ', level * 3);

        await foreach (var (obj, _) in _entryEnumerators[level])
        {
            obj.Fields["id"] = obj.SystemProperties.Id;

            string contentType = _adapter.EnumerateForContentTypes[level].ContentType;

            _scriptObject.SetValue(contentType, obj.Fields, true);

            var filterResult = filterTemplate?.Render(_scriptObject);

            if (filterTemplate is null ||
                (filterResult is not null && filterResult.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                ActionNotifier?.Invoke($"{padding}Processing '{contentType}' - '{obj.Fields["title"]?["en"]}'..");

                _ = await MakeHttpCallsForEnumerators(level + 1, returnVal);
            }
            else
            {
                ActionNotifier?.Invoke($"{padding}Skipping '{contentType}' - '{obj.Fields["title"]?["en"]}'..");
            }

            _scriptObject.Remove(contentType);
        }

        return returnVal;
    }

    private async Task<List<Dictionary<string, string>>> MakeHttpCall(FormUrlEncodedContent? formContent)
    {
        var uriDict = CompileValuesWithEnvironment(new Dictionary<string, string> { ["uri"] = _adapter.EndPoint });

        if (!Uri.IsWellFormedUriString(uriDict["uri"], UriKind.Absolute))
        {
            throw new CliException($"Invalid uri '{_adapter.EndPoint}'");
        }

        _httpClient.DefaultRequestHeaders.Clear();

        if (_adapter.Headers is not null)
        {
            var compiledHeaders = CompileValuesWithEnvironment(_adapter.Headers);

            foreach (var (key, value) in compiledHeaders)
            {
                _httpClient.DefaultRequestHeaders.Add(key, value);
            }
        }

        var returnValue = new List<Dictionary<string, string>>();

        var requestCount = 0;

        var skipTotal = 0;

        var filterTotal = 0;

        var baseAddress = uriDict["uri"];

        while (true)
        {
            requestCount++;

            var getParameters = string.Empty;

            if (_adapter.Pagination is not null)
            {
                getParameters = $"&{_adapter.Pagination.SkipKey}={skipTotal}&{_adapter.Pagination.LimitKey}={_adapter.Pagination.LimitMax}";

                skipTotal += _adapter.Pagination.LimitMax;
            }

            var requestUri = baseAddress + getParameters;

            var results = await GetResponseFromEndpointOrCache(formContent, requestUri, requestCount);

            if (results is null) return [];

            JArray rootArray;

            if (_adapter.ResultsJsonPath is null)
            {
                rootArray = results.ResponseContent as JArray ?? new JArray(results.ResponseContent!)
                    ?? throw new CliException("The result of the endpoint call is not a json array.");
            }
            else if (results.ResponseContent is JObject obj)
            {
                var selectedToken = obj.SelectToken($"$.{_adapter.ResultsJsonPath}");
                rootArray = selectedToken as JArray ?? new JArray(selectedToken!)
                    ?? throw new CliException($"The json path '{_adapter.ResultsJsonPath}' does not exist or is not a json array.");
            }
            else
            {
                throw new CliException($"The result of the endpoint call is not a valid json object or array."); ;
            }

            if (_adapter.FilterExpression is not null)
            {
                var count = rootArray.Count;
                rootArray = FilterResultValues(rootArray);
                filterTotal += count - rootArray.Count;
            }

            ActionNotifier?.Invoke($"...'{requestUri.Snip(40)}' returned {rootArray.Count + returnValue.Count + filterTotal} entries (Filtered={filterTotal})...");

            var batchValue = MapResultValues(rootArray);

            returnValue.AddRange(batchValue);

            if (_adapter.Pagination is not null)
            {
                if (rootArray.Count < _adapter.Pagination.LimitMax)
                {
                    break;
                }
                continue;
            }

            if (_adapter.ContinuationTokenHeader is null) break;

            if (!results.ResponseContentHeaders.ContainsKey(_adapter.ContinuationTokenHeader))
            {
                break;
            }

            if (_httpClient.DefaultRequestHeaders.Contains(_adapter.ContinuationTokenHeader))
            {
                _httpClient.DefaultRequestHeaders.Remove(_adapter.ContinuationTokenHeader);
            }

            var token = results.ResponseContentHeaders[_adapter.ContinuationTokenHeader];

            _httpClient.DefaultRequestHeaders.Add(_adapter.ContinuationTokenHeader, token);
        }

        return [.. returnValue.OrderBy(e => e[_adapter.ContentKeyField])];
    }

    private async Task<HttpResponseCacheEntry?> GetResponseFromEndpointOrCache(
        FormUrlEncodedContent? formContent, string requestUri, int requestCount)
    {
        Task<HttpResponseCacheEntry?> getEntryFunc() => GetResponseFromEndpoint(formContent, requestUri);

        if (_httpResponseFileCache is null) return await getEntryFunc();

        var uri = new Uri(requestUri, UriKind.Absolute);

        var fileBaseName = new StringBuilder();

        fileBaseName.Append(_adapter.Id);
        fileBaseName.Append('_');
        fileBaseName.Append(uri.Host.Replace('.', '-').Trim('-'));
        fileBaseName.Append('_');
        fileBaseName.Append($"{requestCount:D4}");

        if (formContent is not null)
        {
            var hashAlgo = new XxHash3();
            var item = await formContent.ReadAsByteArrayAsync();
            hashAlgo.Append(item);
            fileBaseName.Append('_');
            fileBaseName.Append(hashAlgo.GetCurrentHashAsUInt64());
        }

        return await _httpResponseFileCache.Get(fileBaseName.ToString(), getEntryFunc);
    }

    private async Task<HttpResponseCacheEntry?> GetResponseFromEndpoint(
        FormUrlEncodedContent? formContent, string requestUri)
    {
        HttpResponseMessage? endpointResult = null;

        string? endpointContent = null;

        if (_adapter.HttpMethod == Models.HttpMethod.Post)
        {
            endpointResult = await _httpClient.PostAsync(requestUri, formContent);
        }
        else if (_adapter.HttpMethod == Models.HttpMethod.Get)
        {
            endpointResult = await _httpClient.GetAsync(requestUri);
        }
        else
        {
            throw new NotImplementedException($"Unknown method {_adapter.HttpMethod}");
        }

        endpointResult.EnsureSuccessStatusCode();

        string? optionalSecret = null;
        if (_adapter.ResultsSecret is not null)
        {
            var compiledSecret = CompileValuesWithEnvironment(new Dictionary<string, string> { ["secret"] = _adapter.ResultsSecret });
            optionalSecret = compiledSecret["secret"];
        }

        endpointContent = _adapter.ResultsFormat switch
        {
            ResultsFormat.Json or ResultsFormat.Csv => await endpointResult.Content.ReadAsStringAsync(),

            ResultsFormat.ZippedCsv => await endpointResult.Content.ReadAndUnzipAsCsv(optionalSecret),

            _ => throw new NotImplementedException(),
        };

        if (endpointContent is null) return null;

        var results = _adapter.ResultsFormat switch
        {
            ResultsFormat.Json => JsonConvert.DeserializeObject(endpointContent),

            ResultsFormat.ZippedCsv or ResultsFormat.ZippedCsv => await GetCsvAsJarray(endpointContent),

            _ => throw new NotImplementedException(),
        };

        return new HttpResponseCacheEntry
        {
            RequestUri = requestUri,
            ResponseContent = results,
            ResponseContentHeaders = endpointResult.Headers
                .AsEnumerable()
                .ToDictionary(kv => kv.Key, kv => kv.Value.FirstOrDefault()),
        };
    }

    private static async Task<JArray?> GetCsvAsJarray(string endpointContent)
    {
        var arr = new JArray();
        await foreach (var record in new CsvStringInputAdapter(endpointContent).GetRecordsAsync())
        {
            var obj = new JObject();
            foreach (var (key, value) in record)
            {
                obj[key] = value?.ToString();
            }
            arr.Add(obj);
        }
        return arr;
    }

    private Dictionary<string, string> CompileValuesWithEnvironment(Dictionary<string, string> headers)
    {
        var templates = headers.ToDictionary(m => m.Key, m => Template.Parse(m.Value));

        var errors = new List<string>();

        foreach (var (header, template) in templates)
        {
            if (template.HasErrors)
            {
                errors.Add($"Error(s) in mapping for header '{header}'.{template.Messages.Select(m => $"\n...{m.Message}")} ");
            }
        }

        if (errors.Count != 0) throw new CliException(string.Join('\n', errors));

        try
        {
            var newRecord = templates.ToDictionary(t => t.Key, t => t.Value.Render(_scriptObject));

            return newRecord;
        }
        catch (ScriptRuntimeException e)
        {
            throw new CliException(e.Message, e);
        }
    }

    private List<Dictionary<string, string>> MapResultValues(JArray rootArray)
    {
        try
        {
            var batchValue = rootArray.Cast<JObject>()
                .Select(o =>
                {
                    _scriptObject.SetValue("row", o, true);
                    var vars = _compiledPreTemplates.ToDictionary(t => t.Key, t => t.Value.Render(_scriptObject));
                    _scriptObject.SetValue("var", vars, true);
                    var newRecord = _compiledTemplates.ToDictionary(t => t.Key.Render(_scriptObject), t => t.Value.Render(_scriptObject));
                    _scriptObject.Remove("var");
                    _scriptObject.Remove("row");
                    return newRecord;
                })
                .ToList();

            return batchValue;
        }
        catch (ScriptRuntimeException e)
        {
            throw new CliException(e.Message, e);
        }
    }

    private JArray FilterResultValues(JArray inputValues)
    {
        try
        {
            var filterTemplate = Template.Parse(_adapter.FilterExpression);

            var filteredReturnValues = new JArray(
                inputValues
                .Where(o =>
                {
                    _scriptObject.SetValue("row", o, true);
                    var vars = _compiledPreTemplates.ToDictionary(t => t.Key, t => t.Value.Render(_scriptObject));
                    _scriptObject.SetValue("var", vars, true);
                    var strValue = filterTemplate.Render(_scriptObject);
                    var returnValue = filterTemplate.Render(_scriptObject).Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                    _scriptObject.Remove("var");
                    _scriptObject.Remove("row");
                    return returnValue;
                })
            );

            return filteredReturnValues;
        }
        catch (ScriptRuntimeException e)
        {
            throw new CliException(e.Message, e);
        }
    }

    private static ScriptObject CreateScriptObject(ContentfulConnection contentfulConnection, IReadOnlyDictionary<string, string?> envSettings)
    {
        ScriptObject? scriptObject = [];

        CuteFunctions.ContentfulConnection = contentfulConnection;

        scriptObject.SetValue("cute", new CuteFunctions(), true);

        scriptObject.SetValue("config", envSettings, true);

        return scriptObject;
    }

    private static ContentEntryEnumerators? GetEntryEnumerators(
        List<ContentEntryDefinition>? entryDefinitions,
        ContentfulConnection contentfulConnection,
        IEnumerable<ContentType> contentTypes)
    {
        if (entryDefinitions is null) return null;

        if (entryDefinitions.Count == 0) return null;

        var contentEntryEnumerators = new ContentEntryEnumerators();
        foreach (var entryDefinition in entryDefinitions)
        {
            var contentType = contentTypes.FirstOrDefault(ct => ct.SystemProperties.Id == entryDefinition.ContentType)
                ?? throw new CliException($"Content type '{entryDefinition.ContentType}' does not exist.");

            var enumerator = contentfulConnection.GetManagementEntries<Entry<JObject>>(
                    new EntryQuery.Builder()
                        .WithContentType(entryDefinition.ContentType)
                        .WithOrderByField(contentType.DisplayField)
                        .WithQueryString(entryDefinition.QueryParameters)
                        .Build()
                );

            contentEntryEnumerators.Add(enumerator);
        }

        return contentEntryEnumerators;
    }
}