using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Lib.Contentful;
using Cute.Lib.Scriban;
using Cute.Lib.Serializers;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;

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

    private IDictionary<string, object?>? _flatEntry;

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

            var oldFieldValue = _flatEntry[fieldName]?.ToString();

            CompareAndEdit(newFlatEntry, fieldName, fieldFindValue, fieldReplaceValue, oldFieldValue);
        }

        _scriptObject.Remove(_contentTypeId);

        if (newFlatEntry.Count == 0) return Task.FromResult<IDictionary<string, object?>?>(null);

        newFlatEntry.Add("$id", _flatEntry["$id"]);

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
        var records = ContentfulEntryEnumerator.Entries<Entry<JObject>>(
            _contentfulConnection.ManagementClient,
            _contentTypeId,
            _contentType.DisplayField,
            includeLevels: 1,
            pageSize: 1)
            .ToBlockingEnumerable()
            .Select(s => s.Entries)
            .FirstOrDefault()?.Total ?? 0;

        return Task.FromResult(records);
    }

    public override async IAsyncEnumerable<IDictionary<string, object?>> GetRecordsAsync()
    {
        await foreach (var (entry, _) in ContentfulEntryEnumerator.Entries<Entry<JObject>>(
            _contentfulConnection.ManagementClient, _contentTypeId, _contentType.DisplayField, includeLevels: 1))
        {
            _flatEntry = GetFlatEntry(entry);

            var record = await GetRecordAsync();

            if (record is null || record.Count == 0) continue;

            yield return record;
        }
    }

    private static ScriptObject CreateScriptObject(ContentfulConnection contentfulConnection)
    {
        ScriptObject? scriptObject = [];

        CuteFunctions.ContentfulManagementClient = contentfulConnection.ManagementClient;

        CuteFunctions.ContentfulClient = contentfulConnection.DeliveryClient;

        scriptObject.SetValue("cute", new CuteFunctions(), true);

        return scriptObject;
    }

    private Dictionary<string, object?> GetFlatEntry(Entry<JObject> fullEntry)
    {
        var fullFlatEntry = _serializer.SerializeEntry(fullEntry);

        var end = $".{_locale}";
        var endLat = $".{_locale}.lat";
        var endLon = $".{_locale}.lon";

        var flatEntry = new Dictionary<string, object?>();

        foreach (var (key, value) in fullFlatEntry)
        {
            string newKey;

            if (key.EndsWith(end))
            {
                newKey = key.Replace(end, string.Empty);
            }
            else if (key.EndsWith(endLat) || key.EndsWith(endLon))
            {
                newKey = key.Replace(end, string.Empty).Replace(".", "_");
            }
            else if (key.Equals("sys.Id"))
            {
                newKey = "$id";
            }
            else
            {
                continue;
            }
            flatEntry.Add(newKey, value);
        }

        var end2 = $".{_contentLocales.DefaultLocale}";
        var endLat2 = $".{_contentLocales.DefaultLocale}.lat";
        var endLon2 = $".{_contentLocales.DefaultLocale}.lon";

        foreach (var (key, value) in fullFlatEntry)
        {
            string newKey;

            if (key.EndsWith(end2))
            {
                newKey = key.Replace(end2, string.Empty);
            }
            else if (key.EndsWith(endLat2) || key.EndsWith(endLon2))
            {
                newKey = key.Replace(end2, string.Empty).Replace(".", "_");
            }
            else
            {
                continue;
            }
            if (!flatEntry.TryGetValue(newKey, out _))
            {
                flatEntry[newKey] = value;
                continue;
            }
            flatEntry[newKey] ??= value;
        }

        return flatEntry;
    }
}