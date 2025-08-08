using Contentful.Core.Models;
using Cute.Lib.Cache;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.InputAdapters.Base.Models;
using Cute.Lib.InputAdapters.Http.Models;
using Cute.Lib.Scriban;
using Cute.Lib.Serializers;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Cute.Lib.InputAdapters.Base
{
    public abstract class MappedInputAdapterBase(
        string source,
        DataAdapterConfigBase dataAdapterConfig,
        ContentfulConnection contentfulConnection,
        ContentLocales contentLocales,
        IReadOnlyDictionary<string, string?> envSettings,
        IEnumerable<ContentType> contentTypes)
        : InputAdapterBase(source)
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

        protected Dictionary<string, string> CompileValuesWithEnvironment(Dictionary<string, string> headers)
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

        protected JArray FilterResultValues(JArray inputValues)
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
}
