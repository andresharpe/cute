using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Newtonsoft.Json.Linq;

namespace cut.lib.Serializers;

public class EntrySerializer
{
    private readonly ContentType _contentType;

    private readonly IEnumerable<Locale> _locales;

    private readonly IDictionary<string, EntryFieldSerializer> _fieldSerializers;

    private readonly IDictionary<string, EntryFieldSerializer> _fields;

    public IEnumerable<string> Locales => _locales.Select(l => l.Code);

    public string DefaultLocale => _locales.First(l => l.Default).Code;

    public IEnumerable<string> ColumnFieldNames => _sysFields.Concat(_fieldSerializers.Keys);

    public EntrySerializer(ContentType contentType, IEnumerable<Locale> locales)
    {
        _contentType = contentType;
        _locales = locales;

        _fieldSerializers = new Dictionary<string, EntryFieldSerializer>();
        _fields = new Dictionary<string, EntryFieldSerializer>();

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

    private string[] _sysFields = [
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
        if (flatEntry["sys.UpdatedAt"] is not null) entry.SystemProperties.UpdatedAt = (DateTime)flatEntry["sys.UpdatedAt"]!;
        if (flatEntry["sys.Version"] is not null) entry.SystemProperties.Version = (int)flatEntry["sys.Version"]!;
        if (flatEntry["sys.PublishedVersion"] is not null) entry.SystemProperties.PublishedVersion = (int)flatEntry["sys.PublishedVersion"]!;
        if (flatEntry["sys.PublishedCounter"] is not null) entry.SystemProperties.PublishCounter = (int)flatEntry["sys.PublishedCounter"]!;
        if (flatEntry["sys.PublishedAt"] is not null) entry.SystemProperties.PublishedAt = (DateTime)flatEntry["sys.PublishedAt"]!;
        if (flatEntry["sys.FirstPublishedAt"] is not null) entry.SystemProperties.FirstPublishedAt = (DateTime)flatEntry["sys.FirstPublishedAt"]!;
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
                    values.Add(fieldName, flatEntry[fieldName]);
                }

                newObject.Add(new JProperty(localeCode, entryFieldsSerializer.Deserialize(values)));
            }
            entry.Fields[field.Id] = newObject;
        }

        return entry;
    }
}