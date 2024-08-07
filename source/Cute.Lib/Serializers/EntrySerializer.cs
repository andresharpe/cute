using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Lib.Extensions;
using Newtonsoft.Json.Linq;

namespace Cute.Lib.Serializers;

public class EntrySerializer
{
    private readonly ContentType _contentType;

    private readonly IEnumerable<Locale> _locales;

    private readonly Dictionary<string, EntryFieldSerializer> _fieldSerializers;

    private readonly Dictionary<string, EntryFieldSerializer> _fields;

    public IEnumerable<string> Locales => _locales.Select(l => l.Code);

    public string DefaultLocale => _locales.First(l => l.Default).Code;

    public IEnumerable<string> ColumnFieldNames => _sysFields.Concat(_fieldSerializers.Keys);

    public EntrySerializer(ContentType contentType, IEnumerable<Locale> locales)
    {
        _contentType = contentType;
        _locales = locales;

        _fieldSerializers = [];
        _fields = [];

        var allLocaleCodes = Locales.ToArray();
        var defaultLocaleCodes = new string[] { this.DefaultLocale };

        foreach (var field in _contentType.Fields)
        {
            var localesToProcess = field.Localized ? allLocaleCodes : defaultLocaleCodes;

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

    private readonly string[] _sysFields = [
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

    public IDictionary<string, object?> SerializeEntry(Entry<JObject> entry)
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
            ["sys.ContentType"] = entry.SystemProperties.ContentType.SystemProperties.Id,
            ["sys.Space"] = entry.SystemProperties.Space.SystemProperties.Id,
            ["sys.Environment"] = entry.SystemProperties.Environment.SystemProperties.Id,
        };

        foreach (var (fieldName, fieldSerializer) in _fieldSerializers)
        {
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

        if (flatEntry["sys.Id"] is not null) entry.SystemProperties.Id = (string)flatEntry["sys.Id"]!;
        if (flatEntry["sys.Type"] is not null) entry.SystemProperties.Type = (string)flatEntry["sys.Type"]!;
        if (flatEntry["sys.UpdatedAt"] is not null) entry.SystemProperties.UpdatedAt = ObjectExtensions.FromInvariantDateTime(flatEntry["sys.UpdatedAt"]!);
        if (flatEntry["sys.Version"] is not null) entry.SystemProperties.Version = Convert.ToInt32(flatEntry["sys.Version"]!);
        if (flatEntry["sys.PublishedVersion"] is not null) entry.SystemProperties.PublishedVersion = Convert.ToInt32(flatEntry["sys.PublishedVersion"]!);
        if (flatEntry["sys.PublishedCounter"] is not null) entry.SystemProperties.PublishCounter = Convert.ToInt32(flatEntry["sys.PublishedCounter"]!);
        if (flatEntry["sys.PublishedAt"] is not null) entry.SystemProperties.PublishedAt = ObjectExtensions.FromInvariantDateTime(flatEntry["sys.PublishedAt"]!);
        if (flatEntry["sys.FirstPublishedAt"] is not null) entry.SystemProperties.FirstPublishedAt = ObjectExtensions.FromInvariantDateTime(flatEntry["sys.FirstPublishedAt"]!);
        if (flatEntry["sys.ContentType"] is not null) entry.SystemProperties.ContentType.SystemProperties.Id = (string)flatEntry["sys.ContentType"]!;
        if (flatEntry["sys.Space"] is not null) entry.SystemProperties.Space.SystemProperties.Id = (string)flatEntry["sys.Space"]!;
        if (flatEntry["sys.Environment"] is not null) entry.SystemProperties.Environment.SystemProperties.Id = (string)flatEntry["sys.Environment"]!;

        var allLocaleCodes = Locales.ToArray();
        var defaultLocaleCodes = new string[] { this.DefaultLocale };

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