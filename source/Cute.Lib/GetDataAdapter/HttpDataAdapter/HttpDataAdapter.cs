using Contentful.Core;
using Contentful.Core.Models;
using Cute.Lib.Cache;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.InputAdapters;
using Cute.Lib.Scriban;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using System.IO.Hashing;
using System.Text;

namespace Cute.Lib.GetDataAdapter;

public class HttpDataAdapter
{
    private Action<string>? _displayAction;

    private HttpResponseFileCache? _httpResponseFileCache;

    public HttpDataAdapter WithDisplayAction(Action<string> displayAction)
    {
        _displayAction = displayAction;

        return this;
    }

    public HttpDataAdapter WithHttpResponseFileCache(HttpResponseFileCache httpResponseFileCache)
    {
        _httpResponseFileCache = httpResponseFileCache;

        return this;
    }

    public async Task<List<Dictionary<string, string>>?> GetData(HttpDataAdapterConfig adapter,
            IReadOnlyDictionary<string, string?> envSettings,
            ContentfulManagementClient contentfulManagementClient,
            ContentfulClient contentfulDeliveryClient)
    {
        var compiledTemplates = CompileMappingTemplates(adapter);

        var compiledPreTemplates = CompilePreMappingTemplates(adapter);

        var scriptObject = CreateScripObject(envSettings, contentfulManagementClient, contentfulDeliveryClient);

        if (adapter.EnumerateForContentTypes is not null && adapter.EnumerateForContentTypes.Count != 0)
        {
            var enumerators = adapter.EnumerateForContentTypes
                .Select(contentType =>
                    ContentfulEntryEnumerator.Entries<Entry<JObject>>(contentfulManagementClient, contentType.ContentType, "title",
                        queryString: contentType.QueryParameters)
                ).ToArray();

            return await MakeHttpCallsForEnumerators(adapter, compiledTemplates, compiledPreTemplates, enumerators, scriptObject: scriptObject);
        }

        FormUrlEncodedContent? formContent = null;

        if (adapter.FormUrlEncodedContent is not null)
        {
            formContent = new FormUrlEncodedContent(adapter.FormUrlEncodedContent);
        }

        return await MakeHttpCall(adapter, compiledTemplates, compiledPreTemplates, formContent, scriptObject);
    }

    private async Task<List<Dictionary<string, string>>?> MakeHttpCallsForEnumerators(HttpDataAdapterConfig adapter,
        Dictionary<string, Template> compiledTemplates, Dictionary<string, Template> compiledPreTemplates,
        IAsyncEnumerable<(Entry<JObject>, ContentfulCollection<Entry<JObject>>)>[] enumerators, ScriptObject scriptObject,
        int level = 0, List<Dictionary<string, string>>? returnVal = null)
    {
        if (level > enumerators.Length - 1)
        {
            FormUrlEncodedContent? formContent = null;

            if (adapter.FormUrlEncodedContent is not null)
            {
                var compiledFormUrlEncodedContent = adapter.FormUrlEncodedContent.
                    ToDictionary(kv => kv.Key, kv => Template.Parse(kv.Value).Render(scriptObject));

                formContent = new FormUrlEncodedContent(compiledFormUrlEncodedContent);
            }

            returnVal?.AddRange(await MakeHttpCall(adapter, compiledTemplates, compiledPreTemplates, formContent, scriptObject) ?? []);

            return returnVal;
        }

        returnVal ??= [];

        Template? filterTemplate = null;

        if (adapter.EnumerateForContentTypes[level].Filter is not null)
        {
            filterTemplate = Template.Parse(adapter.EnumerateForContentTypes[level].Filter);
        }

        var padding = new string(' ', level * 3);

        await foreach (var (obj, _) in enumerators[level])
        {
            obj.Fields["id"] = obj.SystemProperties.Id;

            string contentType = adapter.EnumerateForContentTypes[level].ContentType;

            scriptObject.SetValue(contentType, obj.Fields, true);

            var filterResult = filterTemplate?.Render(scriptObject);

            if (filterTemplate is null ||
                (filterResult is not null && filterResult.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                _displayAction?.Invoke($"{padding}Processing '{contentType}' - '{obj.Fields["title"]?["en"]}'..");

                _ = await MakeHttpCallsForEnumerators(adapter, compiledTemplates, compiledPreTemplates, enumerators, scriptObject, level + 1, returnVal);
            }
            else
            {
                _displayAction?.Invoke($"{padding}Skipping '{contentType}' - '{obj.Fields["title"]?["en"]}'..");
            }

            scriptObject.Remove(contentType);
        }

        return returnVal;
    }

    private ScriptObject CreateScripObject(IReadOnlyDictionary<string, string?> contentfulOptions,
        ContentfulManagementClient contentfulManagementClient,
        ContentfulClient contentfulDeliveryClient)
    {
        ScriptObject? scriptObject = [];

        CuteFunctions.ContentfulManagementClient = contentfulManagementClient;

        CuteFunctions.ContentfulClient = contentfulDeliveryClient;

        scriptObject.SetValue("cute", new CuteFunctions(), true);

        scriptObject.SetValue("config", contentfulOptions, true);

        return scriptObject;
    }

    private async Task<List<Dictionary<string, string>>?> MakeHttpCall(HttpDataAdapterConfig adapter,
        Dictionary<string, Template> compiledTemplates, Dictionary<string, Template> compiledPreTemplates,
        FormUrlEncodedContent? formContent,
        ScriptObject scriptObject)
    {
        var uriDict = CompileValuesWithEnvironment(new Dictionary<string, string> { ["uri"] = adapter.EndPoint }, scriptObject);

        if (!Uri.IsWellFormedUriString(uriDict["uri"], UriKind.Absolute))
        {
            throw new CliException($"Invalid uri '{adapter.EndPoint}'");
        }

        var httpClient = new HttpClient();

        if (adapter.Headers is not null)
        {
            var compiledHeaders = CompileValuesWithEnvironment(adapter.Headers, scriptObject);

            foreach (var (key, value) in compiledHeaders)
            {
                httpClient.DefaultRequestHeaders.Add(key, value);
            }
        }

        var returnValue = new List<Dictionary<string, string>>();

        var requestCount = 0;

        var skipTotal = 0;

        var baseAddress = uriDict["uri"];

        while (true)
        {
            requestCount++;

            var getParameters = string.Empty;

            if (adapter.Pagination is not null)
            {
                getParameters = $"&{adapter.Pagination.SkipKey}={skipTotal}&{adapter.Pagination.LimitKey}={adapter.Pagination.LimitMax}";

                skipTotal += adapter.Pagination.LimitMax;
            }

            var requestUri = baseAddress + getParameters;

            var results = await GetResponseFromEndpointOrCache(adapter, httpClient, formContent, requestUri, requestCount, scriptObject);

            if (results is null) return [];

            JArray rootArray = [];

            if (adapter.ResultsJsonPath is null)
            {
                rootArray = results.ResponseContent as JArray
                    ?? throw new CliException("The result of the endpoint call is not a json array.");
            }
            else if (results.ResponseContent is JObject obj)
            {
                rootArray = obj.SelectToken($"$.{adapter.ResultsJsonPath}") as JArray
                    ?? throw new CliException($"The json path '{adapter.ResultsJsonPath}' does not exist or is not a json array.");
            }
            else
            {
                throw new CliException($"The result of the endpoint call is not a valid json object or array."); ;
            }

            if (_displayAction is not null)
            {
                _displayAction($"...'{requestUri}' returned {rootArray.Count} entries...");
            }

            var batchValue = MapResultValues(rootArray, compiledTemplates, compiledPreTemplates, scriptObject);

            returnValue.AddRange(batchValue);

            if (adapter.Pagination is not null)
            {
                if (rootArray.Count < adapter.Pagination.LimitMax)
                {
                    break;
                }
                continue;
            }

            if (adapter.ContinuationTokenHeader is null) break;

            if (!results.ResponseContentHeaders.ContainsKey(adapter.ContinuationTokenHeader))
            {
                break;
            }

            if (httpClient.DefaultRequestHeaders.Contains(adapter.ContinuationTokenHeader))
            {
                httpClient.DefaultRequestHeaders.Remove(adapter.ContinuationTokenHeader);
            }

            var token = results.ResponseContentHeaders[adapter.ContinuationTokenHeader];

            httpClient.DefaultRequestHeaders.Add(adapter.ContinuationTokenHeader, token);
        }

        return [.. returnValue.OrderBy(e => e[adapter.ContentKeyField])];
    }

    private async Task<HttpResponseCacheEntry?> GetResponseFromEndpointOrCache(HttpDataAdapterConfig adapter,
        HttpClient httpClient, FormUrlEncodedContent? formContent, string requestUri, int requestCount, ScriptObject scriptObject)
    {
        Task<HttpResponseCacheEntry?> getEntryFunc() => GetResponseFromEndpoint(adapter, httpClient, formContent, requestUri, scriptObject);

        if (_httpResponseFileCache is null) return await getEntryFunc();

        var uri = new Uri(requestUri, UriKind.Absolute);

        var fileBaseName = new StringBuilder();

        fileBaseName.Append(adapter.Id);
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

    private static async Task<HttpResponseCacheEntry?> GetResponseFromEndpoint(HttpDataAdapterConfig adapter,
        HttpClient httpClient, FormUrlEncodedContent? formContent, string requestUri, ScriptObject scriptObject)
    {
        HttpResponseMessage? endpointResult = null;

        string? endpointContent = null;

        if (adapter.HttpMethod == HttpMethod.Post)
        {
            endpointResult = await httpClient.PostAsync(requestUri, formContent);
        }
        else if (adapter.HttpMethod == HttpMethod.Get)
        {
            endpointResult = await httpClient.GetAsync(requestUri);
        }
        else
        {
            throw new NotImplementedException($"Unknown method {adapter.HttpMethod}");
        }

        endpointResult.EnsureSuccessStatusCode();

        string? optionalSecret = null;
        if (adapter.ResultsSecret is not null)
        {
            var compiledSecret = CompileValuesWithEnvironment(new Dictionary<string, string> { ["secret"] = adapter.ResultsSecret }, scriptObject);
            optionalSecret = compiledSecret["secret"];
        }

        endpointContent = adapter.ResultsFormat switch
        {
            ResultsFormat.Json or ResultsFormat.Csv => await endpointResult.Content.ReadAsStringAsync(),

            ResultsFormat.ZippedCsv => await endpointResult.Content.ReadAndUnzipAsCsv(optionalSecret),

            _ => throw new NotImplementedException(),
        };

        if (endpointContent is null) return null;

        var results = adapter.ResultsFormat switch
        {
            ResultsFormat.Json => JsonConvert.DeserializeObject(endpointContent),

            ResultsFormat.ZippedCsv or ResultsFormat.ZippedCsv => new CsvStringInputAdapter(endpointContent).GetRecords().ToList(),

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

    private static Dictionary<string, string> CompileValuesWithEnvironment(Dictionary<string, string> headers, ScriptObject scriptObject)
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
            var newRecord = templates.ToDictionary(t => t.Key, t => t.Value.Render(scriptObject));

            return newRecord;
        }
        catch (ScriptRuntimeException e)
        {
            throw new CliException(e.Message, e);
        }
    }

    private static Dictionary<string, Template> CompileMappingTemplates(HttpDataAdapterConfig adapter)
    {
        var templates = adapter.Mapping.ToDictionary(m => m.FieldName, m => Template.Parse(m.Expression));

        var errors = new List<string>();

        foreach (var (fieldName, template) in templates)
        {
            if (template.HasErrors)
            {
                errors.Add($"Error(s) in mapping for field '{fieldName}'.{template.Messages.Select(m => $"\n...{m.Message}")} ");
            }
        }

        if (errors.Count != 0) throw new CliException(string.Join('\n', errors));

        return templates;
    }

    private static Dictionary<string, Template> CompilePreMappingTemplates(HttpDataAdapterConfig adapter)
    {
        if (adapter.PreMapping == null) return [];

        var templates = adapter.PreMapping.ToDictionary(m => m.VarName, m => Template.Parse(m.Expression));

        var errors = new List<string>();

        foreach (var (varName, template) in templates)
        {
            if (template.HasErrors)
            {
                errors.Add($"Error(s) in mapping for variable '{varName}'.{template.Messages.Select(m => $"\n...{m.Message}")} ");
            }
        }

        if (errors.Count != 0) throw new CliException(string.Join('\n', errors));

        return templates;
    }

    private static List<Dictionary<string, string>> MapResultValues(JArray rootArray, Dictionary<string, Template> compiledTemplates,
        Dictionary<string, Template> compiledPreTemplates, ScriptObject scriptObject)
    {
        try
        {
            var batchValue = rootArray.Cast<JObject>()
                .Select(o =>
                {
                    scriptObject.SetValue("row", o, true);
                    var vars = compiledPreTemplates.ToDictionary(t => t.Key, t => t.Value.Render(scriptObject));
                    scriptObject.SetValue("var", vars, true);
                    var newRecord = compiledTemplates.ToDictionary(t => t.Key, t => t.Value.Render(scriptObject));
                    scriptObject.Remove("var");
                    scriptObject.Remove("row");
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
}