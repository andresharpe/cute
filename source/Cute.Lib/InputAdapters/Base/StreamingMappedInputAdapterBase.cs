using Contentful.Core.Models;
using Cute.Lib.Cache;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.InputAdapters.Base.Models;
using Cute.Lib.Scriban;
using Cute.Lib.Serializers;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Cute.Lib.InputAdapters.Base;

/// <summary>
/// Base class for streaming input adapters that process large datasets efficiently
/// </summary>
public abstract class StreamingMappedInputAdapterBase(
    string source,
    DataAdapterConfigBase dataAdapterConfig,
    ContentfulConnection contentfulConnection,
    ContentLocales contentLocales,
    IReadOnlyDictionary<string, string?> envSettings,
    IEnumerable<ContentType> contentTypes)
    : InputAdapterBase(source), IStreamingInputAdapter
{
    private readonly DataAdapterConfigBase _adapter = dataAdapterConfig;

    protected readonly ContentLocales _contentLocales = contentLocales;
    protected readonly ContentfulConnection _contentfulConnection = contentfulConnection;
    protected readonly ScriptObject _scriptObject = CreateScriptObject(contentfulConnection, envSettings);
    protected readonly Dictionary<Template, Template> _compiledTemplates = dataAdapterConfig.CompileMappingTemplates();
    protected readonly Dictionary<string, Template> _compiledPreTemplates = dataAdapterConfig.CompilePreMappingTemplates();
    protected readonly ContentEntryEnumerators? _entryEnumerators = GetEntryEnumerators(dataAdapterConfig.EnumerateForContentTypes, contentfulConnection, contentTypes);
    protected readonly IEnumerable<ContentType> _contentTypes = contentTypes;

    protected ContentType _contentType = default!;
    protected EntrySerializer _serializer = default!;

    public abstract Task<int> GetEstimatedRecordCountAsync();
    public abstract IAsyncEnumerable<IEnumerable<IDictionary<string, object?>>> GetRecordBatchesAsync(int batchSize = 1000);

    public virtual async IAsyncEnumerable<IDictionary<string, object?>> GetRecordsStreamAsync()
    {
        await foreach (var batch in GetRecordBatchesAsync(1))
        {
            foreach (var record in batch)
            {
                yield return record;
            }
        }
    }

    public Action<string>? ActionNotifier { get; set; }
    public Action<string>? ErrorNotifier { get; set; }
    public Action<int, int, string?>? CountProgressNotifier { get; set; }

    // Legacy methods for compatibility
    public override async Task<int> GetRecordCountAsync()
    {
        return await GetEstimatedRecordCountAsync();
    }

    public override async Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        // This method is not optimal for streaming, but provided for backward compatibility
        // Consider using GetRecordsStreamAsync() instead
        await foreach (var record in GetRecordsStreamAsync())
        {
            return record;
        }
        return null;
    }

    protected Dictionary<string, string> CompileValuesWithEnvironment(Dictionary<string, string> values)
    {
        var templates = values.ToDictionary(m => m.Key, m => Template.Parse(m.Value));
        var errors = new List<string>();

        foreach (var (key, template) in templates)
        {
            if (template.HasErrors)
            {
                errors.Add($"Error(s) in mapping for key '{key}'.{template.Messages.Select(m => $"\n...{m.Message}")} ");
            }
        }

        if (errors.Count != 0) throw new CliException(string.Join('\n', errors));

        try
        {
            return templates.ToDictionary(t => t.Key, t => t.Value.Render(_scriptObject));
        }
        catch (ScriptRuntimeException e)
        {
            throw new CliException(e.Message, e);
        }
    }

    protected IEnumerable<Dictionary<string, string>> MapResultValues(JArray rootArray)
    {
        try
        {
            foreach (var item in rootArray.Cast<JObject>())
            {
                _scriptObject.SetValue("row", item, true);
                var vars = _compiledPreTemplates.ToDictionary(t => t.Key, t => t.Value.Render(_scriptObject));
                _scriptObject.SetValue("var", vars, true);
                var newRecord = _compiledTemplates.ToDictionary(t => t.Key.Render(_scriptObject), t => t.Value.Render(_scriptObject));
                _scriptObject.Remove("var");
                _scriptObject.Remove("row");
                yield return newRecord;
            }
        }
        catch (ScriptRuntimeException e)
        {
            throw new CliException(e.Message, e);
        }
    }

    protected JArray FilterResultValues(JArray inputValues)
    {
        try
        {
            var filterTemplate = Template.Parse(_adapter.FilterExpression);
            var filteredReturnValues = new JArray(
                inputValues.Where(o =>
                {
                    _scriptObject.SetValue("row", o, true);
                    var vars = _compiledPreTemplates.ToDictionary(t => t.Key, t => t.Value.Render(_scriptObject));
                    _scriptObject.SetValue("var", vars, true);
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

    protected static ScriptObject CreateScriptObject(ContentfulConnection contentfulConnection, IReadOnlyDictionary<string, string?> envSettings)
    {
        ScriptObject? scriptObject = [];
        CuteFunctions.ContentfulConnection = contentfulConnection;
        scriptObject.SetValue("cute", new CuteFunctions(), true);
        scriptObject.SetValue("config", envSettings, true);
        return scriptObject;
    }

    protected static ContentEntryEnumerators? GetEntryEnumerators(
        List<ContentEntryDefinition>? entryDefinitions,
        ContentfulConnection contentfulConnection,
        IEnumerable<ContentType> contentTypes)
    {
        if (entryDefinitions is null || entryDefinitions.Count == 0) return null;

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