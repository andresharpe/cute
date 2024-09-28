using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.Scriban;
using Cute.Lib.Serializers;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using System.Text;

namespace Cute.Lib.InputAdapters.EntryAdapters;

public abstract class FieldsInputAdapterBase(string sourceName, string locale, ContentLocales contentLocales,
    string[] fields, string[] findValues, string[] replaceValues,
    ContentType contentType, ContentfulConnection contentfulConnection) : InputAdapterBase(sourceName)
{
    private readonly string _locale = locale;

    private readonly ContentLocales _contentLocales = new([locale], contentLocales.DefaultLocale);

    private readonly string[] _fields = fields;
    private readonly string[] _findValues = findValues;
    private readonly string[] _replaceValues = replaceValues;

    private JObject? _flatEntry;

    private readonly ContentType _contentType = contentType;

    private readonly string _contentTypeId = contentType.SystemProperties.Id;

    private readonly ContentfulConnection _contentfulConnection = contentfulConnection;

    private readonly ScriptObject _scriptObject = CreateScriptObject(contentfulConnection);

    private readonly EntrySerializer _serializer = new(contentType, new ContentLocales([locale], contentLocales.DefaultLocale));

    private readonly Template[] _compiledFindTemplates = findValues.Select(v => Template.Parse(v)).ToArray();
    private readonly Template[] _compiledReplaceTemplates = replaceValues.Select(v => Template.Parse(v)).ToArray();

    public override Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        if (_flatEntry == null) return Task.FromResult<IDictionary<string, object?>?>(null);

        var newFlatEntry = new Dictionary<string, object?>();

        _scriptObject.SetValue(_contentTypeId, _flatEntry, true);

        for (var i = 0; i < _fields.Length; i++)
        {
            var fieldName = _fields[i];

            var fieldFindValue = string.IsNullOrEmpty(_findValues[i])
                ? null
                : _compiledFindTemplates[i].Render(_scriptObject, memberRenamer: member => member.Name.ToCamelCase());

            var fieldReplaceValue = string.IsNullOrEmpty(_replaceValues[i])
                ? null
                : _compiledReplaceTemplates[i].Render(_scriptObject, memberRenamer: member => member.Name.ToCamelCase());

            var oldFieldValue = _flatEntry.SelectToken(fieldName)?.ToString();

            CompareAndEdit(newFlatEntry, fieldName, fieldFindValue, fieldReplaceValue, oldFieldValue);
        }

        _scriptObject.Remove(_contentTypeId);

        if (newFlatEntry.Count == 0) return Task.FromResult<IDictionary<string, object?>?>(null);

        newFlatEntry.Add("$id", _flatEntry.SelectToken("sys.id"));

        var finalFlatEntry = new Dictionary<string, object?>();
        foreach (var (key, value) in newFlatEntry)
        {
            if (key.Equals("$id"))
            {
                finalFlatEntry.Add("sys.Id", value);
                continue;
            }

            if (key.Contains('_'))
            {
                finalFlatEntry.Add(key.Replace("_", $".{_locale}."), value);
                continue;
            }

            finalFlatEntry.Add($"{key}.{_locale}", value);
        }

        if (!finalFlatEntry.ContainsKey($"{_contentType.DisplayField}.{_contentLocales.DefaultLocale}"))
        {
            finalFlatEntry.Add($"{_contentType.DisplayField}.{_contentLocales.DefaultLocale}", _flatEntry[_contentType.DisplayField]);
        }

        return Task.FromResult<IDictionary<string, object?>?>(finalFlatEntry);
    }

    protected abstract void CompareAndEdit(Dictionary<string, object?> newFlatEntry, string fieldName, string? fieldFindValue, string? fieldReplaceValue, string? oldFieldValue);

    public override Task<int> GetRecordCountAsync()
    {
        var records = _contentfulConnection.GetManagementEntries<Entry<JObject>>(
                new EntryQuery.Builder()
                    .WithContentType(_contentTypeId)
                    .WithPageSize(1)
                    .WithIncludeLevels(0)
                    .Build()
            )
            .ToBlockingEnumerable()
            .Select(s => s.TotalEntries)
            .First();

        return Task.FromResult(records);
    }

    public override async IAsyncEnumerable<IDictionary<string, object?>> GetRecordsAsync()
    {
        var sb = new StringBuilder();
        foreach (var field in _fields.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            sb.AppendLine($"{{{{ {_contentTypeId}.{field} }}}}");
        }
        foreach (var field in _findValues.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            sb.AppendLine($"{field}");
        }
        foreach (var field in _replaceValues.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            sb.AppendLine($"{field}");
        }
        var autoQueryBuilder = _contentfulConnection.GraphQL.CreateAutoQueryBuilder()
            .WithTemplateContent(sb.ToString())
            .WithExtraVariables([[_contentTypeId, _contentType.DisplayField]]);

        if (!autoQueryBuilder.TryBuildQuery(out var query) || query is null)
        {
            var errors = string.Join('\n', autoQueryBuilder.Errors);
            throw new CliException($"Error building query: {errors}");
        }

        var enumerable = _contentfulConnection.GraphQL.GetDataEnumerable(
            query,
            $"data.{autoQueryBuilder.ContentTypeId}Collection.items",
            _locale,
            preview: true
        );

        //var enumerable = _contentfulConnection.GetManagementEntries<Entry<JObject>>(
        //    new EntryQuery.Builder()
        //        .WithContentType(_contentType)
        //        .WithIncludeLevels(2)
        //        .Build()
        //    );

        await foreach (var entry in enumerable)
        {
            _flatEntry = entry;

            var record = await GetRecordAsync();

            if (record is null || record.Count == 0) continue;

            yield return record;
        }
    }

    private static ScriptObject CreateScriptObject(ContentfulConnection contentfulConnection)
    {
        ScriptObject? scriptObject = [];

        CuteFunctions.ContentfulConnection = contentfulConnection;

        scriptObject.SetValue("cute", new CuteFunctions(), true);

        return scriptObject;
    }
}