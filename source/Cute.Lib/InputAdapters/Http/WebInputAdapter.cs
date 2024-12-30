using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.InputAdapters.Http.Models;
using Cute.Lib.Scriban;
using Cute.Lib.Serializers;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Cute.Lib.InputAdapters.Http;

public abstract class WebInputAdapter(
    string sourceName,
    BaseDataAdapterConfig adapter,
    ContentfulConnection contentfulConnection,
    IReadOnlyDictionary<string, string?> envSettings)
    : InputAdapterBase(sourceName)
{
    protected readonly ScriptObject _scriptObject = CreateScriptObject(contentfulConnection, envSettings);

    protected readonly Dictionary<Template, Template> _compiledTemplates = adapter.CompileMappingTemplates();

    protected readonly Dictionary<string, Template> _compiledPreTemplates = adapter.CompilePreMappingTemplates();

    protected ContentType _contentType = default!;

    protected List<Dictionary<string, string>> _results = default!;

    protected EntrySerializer _serializer = default!;

    protected int _currentRecordIndex = -1;

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

    protected List<Dictionary<string, string>> MapResultValues(JArray rootArray)
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

    protected static ScriptObject CreateScriptObject(ContentfulConnection contentfulConnection, IReadOnlyDictionary<string, string?> envSettings)
    {
        ScriptObject? scriptObject = [];

        CuteFunctions.ContentfulConnection = contentfulConnection;

        scriptObject.SetValue("cute", new CuteFunctions(), true);

        scriptObject.SetValue("config", envSettings, true);

        return scriptObject;
    }
}