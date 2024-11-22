using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Cute.Lib.Extensions;
using Newtonsoft.Json.Linq;
using System.Collections.Immutable;

namespace Cute.Lib.Serializers;

public class EntrySerializer
{
    private readonly ContentType _contentType;

    private readonly Dictionary<string, EntryFieldSerializer> _fieldSerializers;

    private readonly Dictionary<string, EntryFieldSerializer> _fields;

    private readonly string[] _locales;

    private static readonly List<string> _sysFields = [
        "sys.Id",
        "sys.Type",
        "sys.UpdatedAt",
        "sys.Version",
        "sys.PublishedVersion",
        "sys.PublishedCounter",
        "sys.PublishedAt",
        "sys.FirstPublishedAt",
        "sys.ContentType",
        "sys.Space",
        "sys.Environment",
    ];

    public static readonly ImmutableHashSet<string> SysFields = ImmutableHashSet.CreateRange(_sysFields);

    public IEnumerable<string> ColumnFieldNames => _sysFields.Concat(_fieldSerializers.Keys);

    public EntrySerializer(ContentType contentType, ContentLocales contentLocales)
    {
        _contentType = contentType;
        _locales = contentLocales.GetAllLocales();
        _fieldSerializers = [];
        _fields = [];
        
        var allLocaleCodes = _locales;

        string[] defaultLocaleCodes = [contentLocales.DefaultLocale];

        foreach (var field in _contentType.Fields)
        {
            var localesToProcess = field.Localized
                ? allLocaleCodes
                : defaultLocaleCodes;

            foreach (var localeCode in localesToProcess)
            {
                var entryFieldSerializer = new EntryFieldSerializer(localeCode, field);

                var fieldNames = entryFieldSerializer.FullFieldNames();

                _fields.Add(field.Id + "." + localeCode, entryFieldSerializer);

                foreach (var fieldName in fieldNames)
                {
                    _fieldSerializers.Add(fieldName, entryFieldSerializer);
                }
            }
        }
    }

    public Dictionary<string, object?> CreateNewFlatEntry()
    {
        var newId = ContentfulIdGenerator.NewId();

        return new Dictionary<string, object?>()
        {
            ["sys.Id"] = newId,
            ["sys.Type"] = null,
            ["sys.UpdatedAt"] = null,
            ["sys.Version"] = 0,
            ["sys.PublishedVersion"] = null,
            ["sys.PublishedCounter"] = null,
            ["sys.PublishedAt"] = null,
            ["sys.FirstPublishedAt"] = null,
            ["sys.ContentType"] = _contentType.SystemProperties.Id,
            ["sys.Space"] = null,
            ["sys.Environment"] = null,
        };
    }

    public IDictionary<string, object?> CreateNewFlatEntry(IDictionary<string, string> flatEntryDefaults)
    {
        var flatEntry = CreateNewFlatEntry();
        foreach (var (key, value) in flatEntryDefaults)
        {
            flatEntry[key] = value;
        }
        return SerializeEntry(DeserializeEntry(flatEntry)).Where(o => o.Key.StartsWith("sys.") || o.Value is not null).ToDictionary();
    }

    public IDictionary<string, object?> SerializeEntry(Entry<JObject> entry, bool includeMissingFieldsInEntry = true)
    {
        var leanEntry = new Dictionary<string, object?>
        {
            ["sys.Id"] = entry.SystemProperties.Id,
            ["sys.Type"] = entry.SystemProperties.Type,
            ["sys.UpdatedAt"] = entry.SystemProperties.UpdatedAt,
            ["sys.Version"] = entry.SystemProperties.Version,
            ["sys.PublishedVersion"] = entry.SystemProperties.PublishedVersion,
            ["sys.PublishedCounter"] = entry.SystemProperties.PublishCounter,
            ["sys.PublishedAt"] = entry.SystemProperties.PublishedAt,
            ["sys.FirstPublishedAt"] = entry.SystemProperties.FirstPublishedAt,
            ["sys.ContentType"] = entry.SystemProperties.ContentType?.SystemProperties.Id,
            ["sys.Space"] = entry.SystemProperties.Space?.SystemProperties.Id,
            ["sys.Environment"] = entry.SystemProperties.Environment?.SystemProperties.Id,
        };

        foreach (var (fieldName, fieldSerializer) in _fieldSerializers)
        {
            if (!includeMissingFieldsInEntry
                && !entry.Fields.ContainsKey(fieldName)
                && !entry.Fields.ContainsKey(fieldSerializer.Name))
            {
                continue;
            }

            leanEntry[fieldName] = fieldSerializer.Serialize(entry.Fields, fieldName);
        }

        return leanEntry;
    }

    public Entry<JObject> DeserializeEntry(IDictionary<string, object?> flatEntry)
    {
        var entry = new Entry<JObject>()
        {
            SystemProperties = new()
            {
                ContentType = new()
                {
                    SystemProperties = new()
                },
                Space = new()
                {
                    SystemProperties = new()
                },
                Environment = new()
                {
                    SystemProperties = new()
                },
            },
            Fields = [],
            Metadata = new()
        };

        if (flatEntry.TryGetValue("sys.Id",
            out var obj) && obj is not null) entry.SystemProperties.Id = obj.ToString();
        if (flatEntry.TryGetValue("sys.Type",
            out obj) && obj is not null) entry.SystemProperties.Type = obj.ToString();
        if (flatEntry.TryGetValue("sys.UpdatedAt",
            out obj) && obj is not null) entry.SystemProperties.UpdatedAt = ObjectExtensions.FromInvariantDateTime(obj);
        if (flatEntry.TryGetValue("sys.Version",
            out obj) && obj is not null) entry.SystemProperties.Version = Convert.ToInt32(obj);
        if (flatEntry.TryGetValue("sys.PublishedVersion",
            out obj) && obj is not null) entry.SystemProperties.PublishedVersion = Convert.ToInt32(obj);
        if (flatEntry.TryGetValue("sys.PublishedCounter",
            out obj) && obj is not null) entry.SystemProperties.PublishCounter = Convert.ToInt32(obj);
        if (flatEntry.TryGetValue("sys.PublishedAt",
            out obj) && obj is not null) entry.SystemProperties.PublishedAt = ObjectExtensions.FromInvariantDateTime(obj);
        if (flatEntry.TryGetValue("sys.FirstPublishedAt",
            out obj) && obj is not null) entry.SystemProperties.FirstPublishedAt = ObjectExtensions.FromInvariantDateTime(obj);
        if (flatEntry.TryGetValue("sys.ContentType",
            out obj) && obj is not null) entry.SystemProperties.ContentType.SystemProperties.Id = obj.ToString();
        if (flatEntry.TryGetValue("sys.Space",
            out obj) && obj is not null) entry.SystemProperties.Space.SystemProperties.Id = obj.ToString();
        if (flatEntry.TryGetValue("sys.Environment",
            out obj) && obj is not null) entry.SystemProperties.Environment.SystemProperties.Id = obj.ToString();

        var allLocaleCodes = _locales;
        var defaultLocaleCodes = _locales[0..1];

        foreach (var field in _contentType.Fields)
        {
            var localesToProcess = field.Localized ? allLocaleCodes : defaultLocaleCodes;

            var newObject = new JObject();

            foreach (var localeCode in localesToProcess)
            {
                var entryFieldsSerializer = _fields[field.Id + "." + localeCode];

                var fieldNames = entryFieldsSerializer.FullFieldNames();

                Dictionary<string, object?> values = [];

                foreach (var fieldName in fieldNames)
                {
                    if (flatEntry.TryGetValue(fieldName, out var value))
                    {
                        values.Add(fieldName, value);
                    }
                    else
                    {
                        values.Add(fieldName, null);
                    }
                }

                newObject.Add(new JProperty(localeCode, entryFieldsSerializer.Deserialize(values)));
            }

            entry.Fields[field.Id] = newObject;
        }

        return entry;
    }

    public bool CompareAndUpdateEntry<T>(IDictionary<string, object?> flatEntry, string fieldName, T newValue)
    {
        if (!flatEntry.TryGetValue(fieldName, out var oldValue))
        {
            return false;
        }

        var entryFieldsSerializer = _fieldSerializers[fieldName];

        var isEqual = entryFieldsSerializer.Compare(oldValue, newValue);

        if (!isEqual)
        {
            flatEntry[fieldName] = newValue;
            return true;
        }

        return false;
    }
}