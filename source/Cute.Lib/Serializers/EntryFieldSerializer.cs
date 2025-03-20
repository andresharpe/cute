﻿using Contentful.Core.Configuration;
using Contentful.Core.Errors;
using Contentful.Core.Extensions;
using Contentful.Core.Models;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Html2Markdown;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Dynamic;

namespace Cute.Lib.Serializers;

internal class EntryFieldSerializer
{
    private const char _arrayDelimeter = '|';
    private const char _arrayCfDelimeter = ',';

    private static readonly JsonSerializerSettings _jsonSettings = new() { Converters = [new ContentJsonConverter()] };

    private readonly string _name;
    private readonly FieldType _contentfulType;
    private readonly Schema _itemType;
    private readonly string _linkType;
    public readonly string _localeCode;
    public readonly string _postfix;
    public readonly string _fullFieldName;
    public readonly bool _localized;
    public readonly Type _dotnetType;

    public bool IsLocalized => _localized;

    public string Name => _name;

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

        _dotnetType = ToDotnetType();
    }

    private Type ToDotnetType()
    {
        return _contentfulType switch
        {
            FieldType.Symbol => typeof(string),
            FieldType.Text => typeof(string),
            FieldType.RichText => typeof(JObject),
            FieldType.Integer => typeof(long),
            FieldType.Number => typeof(double),
            FieldType.Date => typeof(DateTime),
            FieldType.Location => typeof(JObject),
            FieldType.Boolean => typeof(bool),
            FieldType.Link => typeof(JObject),
            FieldType.Array => typeof(JArray),
            FieldType.Object => typeof(JObject),
            _ => throw new NotImplementedException(),
        };
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
        // assume delivery or preview entry
        JToken? prop = entry[_name];

        if (prop is null || prop.IsNull())
        {
            return null;
        }

        if (prop is JObject obj)
        {
            // but if may be a management entry
            prop = obj[_localeCode];

            if (prop is null || prop.IsNull())
            {
                if (_contentfulType == FieldType.Link && obj["sys"] != null)
                {
                    prop = obj;
                }
                else if (_contentfulType == FieldType.Location && obj["lat"] != null)
                {
                    prop = obj;
                }
                else
                {
                    return null;
                }
            }
        }

        return _contentfulType switch
        {
            FieldType.Symbol => prop.Value<string>(),
            FieldType.Text => prop.Value<string>(),
            FieldType.RichText => ToMarkDown(prop),
            FieldType.Integer => prop.Value<long>(),
            FieldType.Number => prop.Value<double>(),
            FieldType.Date => prop.Value<DateTime>(),
            FieldType.Location => prop[fieldName[^3..]]?.Value<double>(),
            FieldType.Boolean => prop.Value<bool>(),
            FieldType.Link => prop["sys"]?["id"]?.Value<string>(),
            FieldType.Array => ToArrayString(prop),
            FieldType.Object => prop.ToString(),
            _ => throw new NotImplementedException(),
        };
    }

    public JToken? Deserialize(IDictionary<string, object?> values)
    {
        var value = values.First().Value;

        if (value == null) return null;
        try
        {
            return _contentfulType switch
            {
                FieldType.Symbol => value?.ToString(),
                FieldType.Text => value?.ToString(),
                FieldType.RichText => ToDocument(value),
                FieldType.Integer => Convert.ToInt64(value is string s && string.IsNullOrWhiteSpace(s) ? null : value),
                FieldType.Number => Convert.ToDouble(value is string s && string.IsNullOrWhiteSpace(s) ? null : value),
                FieldType.Date => ToDateTime(value),
                FieldType.Location => ToLocation(values),
                FieldType.Boolean => string.IsNullOrWhiteSpace(value.ToString()) ? null : Convert.ToBoolean(value),
                FieldType.Link => ToLink(value),
                FieldType.Array => ToObjectArray(value),
                FieldType.Object => JsonConvert.DeserializeObject<JToken>((string)value),
                _ => throw new NotImplementedException(),
            };
        }
        catch (Exception ex)
        {
            throw new CliException($"Field: '{_fullFieldName}' ({_contentfulType}) {ex.Message}", ex);
        }
    }

    public string? DeserializeToString(object? value)
    {
        if (value == null) return null;

        if (value is string stringValue && string.IsNullOrWhiteSpace(stringValue)) return null;

        return _contentfulType switch
        {
            FieldType.Symbol => value.ToString(),
            FieldType.Text => value.ToString(),
            FieldType.RichText => ToDocument(value)?.ToString(),
            FieldType.Integer => Convert.ToInt64(value).ToString(),
            FieldType.Number => Convert.ToDouble(value).ToString(),
            FieldType.Date => ToDateTime(value)?.ToString("u"),
            FieldType.Location => Convert.ToDouble(value).ToString(),
            FieldType.Boolean => string.IsNullOrWhiteSpace(value.ToString()) ? null : Convert.ToBoolean(value).ToString(),
            FieldType.Link => ToLink(value)?.ToString(),
            FieldType.Array => ToObjectArray(value)?.ToString(),
            FieldType.Object => ToObjectString(value),
            _ => throw new NotImplementedException(),
        };
    }

    internal bool Compare<T>(object? value1, T? value2)
    {
        if (_contentfulType == FieldType.Object)
        {
            if (value1 is string str1 && value2 is string str2)
            {
                var obj1 = JsonConvert.DeserializeObject<JToken>(str1);
                var obj2 = JsonConvert.DeserializeObject<JToken>(str2);
                return JToken.DeepEquals(obj1, obj2);
            }
            else if (value1 is null && value2 is null)
            {
                return true;
            }
            return false;
        }
        else if (false && _contentfulType == FieldType.Array) // skip for now, the below string comparison should work fine
        {
            if (value1 is string str1 && value2 is string str2)
            {
                var arr1 = ToObjectArray(str1);
                var arr2 = ToObjectArray(str2);

                if (arr1 is null || arr2 is null) return false;

                return AreEquivalent(arr1, arr2);
            }
            else if (value1 is null && value2 is null)
            {
                return true;
            }
        }

        var normalizedValue1 = DeserializeToString(value1);
        var normalizedValue2 = DeserializeToString(value2);

        return normalizedValue1 == normalizedValue2;
    }

    public static bool AreEquivalent(JArray array1, JArray array2)
    {
        if (array1.Count != array2.Count)
        {
            return false;
        }

        var valueCounts = new Dictionary<string, int>();

        foreach (var token in array1)
        {
            string key = token.ToString(Formatting.None);
            if (valueCounts.TryGetValue(key, out int value))
            {
                valueCounts[key] = ++value;
            }
            else
            {
                valueCounts[key] = 1;
            }
        }

        foreach (var token in array2)
        {
            string key = token.ToString(Formatting.None);
            if (!valueCounts.TryGetValue(key, out int value))
            {
                return false;
            }

            valueCounts[key] = --value;

            if (value < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static DateTime? ToDateTime(object? value)
    {
        if (value is null) return null;

        var date = ObjectExtensions.FromInvariantDateTime(value);

        if (date == DateTime.MinValue) return null;

        // Contentful doesn't support milliseconds although it states it is ISO 8601 compliant :(
        return date.StripMilliseconds();
    }

    private static string? ToObjectString(object? value)
    {
        if (value is string stringValue)
        {
            return stringValue;
        }
        else if (value is JToken jsonValue)
        {
            return jsonValue.ToString();
        }
        return JsonConvert.SerializeObject(value);
    }

    private JObject? ToLink(object value)
    {
        if (value is string linkValue)
        {
            if (string.IsNullOrEmpty(linkValue)) return null;
        }

        var obj = new
        {
            sys = new
            {
                type = "Link",
                linkType = _linkType ?? "Entry",
                id = value,
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

            string[] arr;

            if (stringValue.Contains(_arrayDelimeter))
            {
                arr = stringValue.Split(_arrayDelimeter).Select(s => s.Trim()).ToArray();
            }
            else if (stringValue.Contains(_arrayCfDelimeter))
            {
                arr = stringValue.Split(_arrayCfDelimeter).Select(s => s.Trim()).ToArray();
            }
            else
            {
                arr = [stringValue];
            }

            foreach (var arrayItem in arr)
            {
                if (_itemType.Type == "Link")
                {
                    var item = ToLink(arrayItem);
                    if (item is not null)
                    {
                        obj.Add(item);
                    }
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

    private static JObject? ToDocument(object? value)
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
                    Marks = [],
                    Value = (string)value
                }]
            }]
        };

        var jObj = JObject
            .FromObject(doc)
            .ToObject<ExpandoObject>();

        if (jObj is null) return null;

        return JObject.FromObject(jObj, JsonSerializer.Create(new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));
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