using Contentful.Core.Configuration;
using Contentful.Core.Errors;
using Contentful.Core.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using cut.lib.Enums;
using Html2Markdown;

namespace cut.lib.Serializers;

internal class EntryFieldSerializer
{
    private const char _arrayDelimeter = ',';

    private static readonly JsonSerializerSettings _jsonSettings = new() { Converters = [new ContentJsonConverter()] };

    private readonly string _name;
    private readonly FieldType _contentfulType;
    private readonly Schema _itemType;
    private readonly string _linkType;
    public readonly string _localeCode;
    public readonly string _postfix;
    public readonly string _fullFieldName;
    public readonly bool _localized;

    public bool IsLocalized => _localized;

    public EntryFieldSerializer(string localeCode, Field contentfulFieldDef)
    {
        _localeCode = localeCode;

        if (!Enum.TryParse(contentfulFieldDef.Type, out FieldType fldType))
        {
            throw new ContentfulException(0, $"Invalid field type '{contentfulFieldDef.Type}'");
        }

        _name = contentfulFieldDef.Id;
        _contentfulType = fldType;
        _itemType = contentfulFieldDef.Items;
        _linkType = contentfulFieldDef.LinkType;
        _postfix = string.Empty;
        _localized = contentfulFieldDef.Localized;

        if (_contentfulType == FieldType.Array)
        {
            _postfix = "[]";
        }

        _fullFieldName = _name + "." + _localeCode + _postfix;
    }

    public string[] FullFieldNames()
    {
        if (_contentfulType == FieldType.Location)
        {
            return [_fullFieldName + ".lat", _fullFieldName + ".lon"];
        }
        return [_fullFieldName];
    }

    public object? Serialize(JObject entry, string fieldName)
    {
        return _contentfulType switch
        {
            FieldType.Symbol => entry[_name]?[_localeCode]?.Value<string>(),
            FieldType.Text => entry[_name]?[_localeCode]?.Value<string>(),
            FieldType.RichText => ToMarkDown(entry[_name]?[_localeCode]),
            FieldType.Integer => entry[_name]?[_localeCode]?.Value<long>(),
            FieldType.Number => entry[_name]?[_localeCode]?.Value<double>(),
            FieldType.Date => entry[_name]?[_localeCode]?.Value<DateTime>(),
            FieldType.Location => entry[_name]?[_localeCode]?[fieldName[^3..]]?.Value<double>(),
            FieldType.Boolean => entry[_name]?[_localeCode]?.Value<bool>(),
            FieldType.Link => entry[_name]?[_localeCode]?["sys"]?["id"]?.Value<string>(),
            FieldType.Array => ToArrayString(entry[_name]?[_localeCode]),
            FieldType.Object => entry[_name]?[_localeCode]?.ToString(),
            _ => throw new NotImplementedException(),
        };
    }

    public JToken? Deserialize(IDictionary<string, object?> values)
    {
        var value = values.First().Value;

        if (value == null) return null;

        return _contentfulType switch
        {
            FieldType.Symbol => value?.ToString(),
            FieldType.Text => value?.ToString(),
            FieldType.RichText => ToDocument(value),
            FieldType.Integer => Convert.ToInt64(value),
            FieldType.Number => Convert.ToDouble(value),
            FieldType.Date => Convert.ToDateTime(value),
            FieldType.Location => ToLocation(values),
            FieldType.Boolean => Convert.ToBoolean(value),
            FieldType.Link => ToLink(value),
            FieldType.Array => ToObjectArray(value),
            FieldType.Object => JObject.Parse((string)value),
            _ => throw new NotImplementedException(),
        };
    }

    private JObject? ToLink(object value)
    {
        var obj = new
        {
            sys = new
            {
                type = "Link",
                id = value,
                linkType = _linkType
            }
        };
        return JObject.FromObject(obj);
    }

    private static JObject? ToLocation(IDictionary<string, object?> values)
    {
        if (values.Count != 2) return null;

        var result = new JObject();

        var first = values.First();
        result.Add(new JProperty(first.Key[^3..], Convert.ToDouble(first.Value)));

        var last = values.Last();
        result.Add(new JProperty(last.Key[^3..], Convert.ToDouble(last.Value)));

        return result;
    }

    private JArray? ToObjectArray(object? value)
    {
        if (value is string stringValue)
        {
            var obj = new JArray();
            var arr = stringValue.Split(_arrayDelimeter);
            foreach (var arrayItem in arr)
            {
                if (_itemType.Type == "Link")
                {
                    var item = new JObject();
                    item["id"] = arrayItem;
                    item["type"] = "Link";
                    item["linkType"] = _itemType.LinkType;
                    obj.Add(item);
                }
                else
                {
                    obj.Add(arrayItem);
                }
            }
            return obj;
        }
        return null;
    }

    private JObject? ToDocument(object? value)
    {
        if (value is null) return null;

        var doc = new Document()
        {
            NodeType = "document",
            Data = new(),
            Content = [new Paragraph()
            {
                NodeType = "paragraph",
                Data = new(),
                Content = [new Text()
                {
                    NodeType = "text",
                    Data = new(),
                    Marks = new(),
                    Value = (string)value
                }]
            }]
        };

        return JObject.FromObject(doc);
    }

    private string? ToArrayString(JToken? list)
    {
        if (list == null) return null;

        if (_itemType == null) return null;

        if (_itemType.Type == "Link")
        {
            return string.Join(_arrayDelimeter, list.Select(t => t["sys"]?["id"]?.ToString()));
        }

        return string.Join(_arrayDelimeter, list.Select(o => o.ToString()));
    }

    private static string? ToMarkDown(JToken? richText)
    {
        if (richText == null) return null;

        var document = JsonConvert.DeserializeObject<Document>(richText.ToString(), _jsonSettings); ;

        if (document is null) return null;

        var _htmlRenderer = new HtmlRenderer();

        var html = _htmlRenderer.ToHtml(document).Result;

        var converter = new Converter();

        return converter.Convert(html);
    }
}