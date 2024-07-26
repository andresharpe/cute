using Cute.Lib.Exceptions;
using Cute.Lib.Scriban;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Scriban.Runtime;
using Scriban.Syntax;
using Scriban;
using Contentful.Core;
using dotenv.net;
using System.Collections;

namespace Cute.Lib.GetDataAdapter;

public class HttpDataAdapter
{
    private static readonly Dictionary<string, string?> _env;

    static HttpDataAdapter()
    {
        _env = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => e.Value?.ToString());

        foreach (var (key, value) in DotEnv.Fluent().Read())
        {
            _env[key] = value;
        }
    }

    private readonly ContentfulManagementClient _contentfulManagementClient;
    private readonly Action<string> _displayAction;

    public HttpDataAdapter(ContentfulManagementClient contentfulManagementClient, Action<string> displayAction)
    {
        _contentfulManagementClient = contentfulManagementClient;

        _displayAction = displayAction;
    }

    public async Task<List<Dictionary<string, string>>?> GetData(HttpDataAdapterConfig adapter)
    {
        if (!Uri.IsWellFormedUriString(adapter.EndPoint, UriKind.Absolute))
        {
            throw new CliException($"Invalid uri '{adapter.EndPoint}'");
        }

        var uriDict = CompileValues(new Dictionary<string, string> { ["uri"] = adapter.EndPoint });

        var httpClient = new HttpClient()
        {
            BaseAddress = new Uri(uriDict["uri"]),
        };

        if (adapter.Headers is not null)
        {
            var compiledHeaders = CompileValues(adapter.Headers);

            foreach (var (key, value) in compiledHeaders)
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

        var requestCount = 1;

        var compiledTemplates = CompileMappingTemplates(adapter);

        while (true)
        {
            HttpResponseMessage? endpointResult = null;

            string? endpointContent = null;

            if (adapter.HttpMethod == HttpMethod.Post)
            {
                endpointResult = await httpClient.PostAsync("", formContent);
            }
            else if (adapter.HttpMethod == HttpMethod.Get)
            {
                endpointResult = await httpClient.GetAsync("");
            }

            if (endpointResult is not null)
            {
                endpointContent = await endpointResult.Content.ReadAsStringAsync();
            }

            if (endpointContent is null) return [];

            var results = JsonConvert.DeserializeObject(endpointContent);

            if (results is null) return null;

            JArray rootArray = [];

            if (adapter.ResultsJsonPath is null)
            {
                rootArray = results as JArray
                    ?? throw new CliException("The result of the endpoint call is not a json array.");
            }
            else if (results is JObject obj)
            {
                rootArray = obj.SelectToken($"$.{adapter.ResultsJsonPath}") as JArray
                    ?? throw new CliException($"The json path '{adapter.ResultsJsonPath}' does not exist or is not a json array.");
            }
            else
            {
                throw new CliException($"The result of the endpoint call is not a valid json object or array."); ;
            }

            _displayAction($"...{httpClient.BaseAddress.Host} returned {rootArray.Count} entries...");

            var batchValue = MapResultValues(rootArray, compiledTemplates);

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

    private static Dictionary<string, string> CompileValues(Dictionary<string, string> headers)
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
            var scriptObjectGlobal = new ScriptObject();

            scriptObjectGlobal.SetValue("cute", new CuteFunctions(), true);

            var templateContext = new TemplateContext();

            templateContext.PushGlobal(scriptObjectGlobal);

            var scriptObjectInstance = new ScriptObject();

            scriptObjectInstance.Import(new { config = new { env = _env } });

            templateContext.PushGlobal(scriptObjectInstance);

            var newRecord = templates.ToDictionary(t => t.Key, t => t.Value.Render(templateContext));

            templateContext.PopGlobal();

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

    private List<Dictionary<string, string>> MapResultValues(JArray rootArray, Dictionary<string, Template> templates)
    {
        CuteFunctions.ContentfulManagementClient ??= _contentfulManagementClient;

        try
        {
            var scriptObjectGlobal = new ScriptObject();

            scriptObjectGlobal.SetValue("cute", new CuteFunctions(), true);

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

            return batchValue;
        }
        catch (ScriptRuntimeException e)
        {
            throw new CliException(e.Message, e);
        }
    }
}