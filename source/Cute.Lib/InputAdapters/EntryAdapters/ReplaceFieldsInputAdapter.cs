using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Cute.Lib.InputAdapters.EntryAdapters;

namespace Cute.Lib.InputAdapters;

public class ReplaceFieldsInputAdapter(string locale,
    ContentLocales contentLocales, string[] fields, string[] replaceValues,
    ContentType contentType, ContentfulConnection contentfulConnection)

    : FieldsInputAdapterBase(nameof(ReplaceFieldsInputAdapter),
        locale, contentLocales, fields, new string[replaceValues.Length], replaceValues, contentType, contentfulConnection)
{
    protected override void CompareAndEdit(Dictionary<string, object?> newFlatEntry, string fieldName, string? fieldFindValue, string? fieldReplaceValue, string? oldFieldValue)
    {
        if (oldFieldValue != fieldReplaceValue)
        {
            newFlatEntry.Add(fieldName, fieldReplaceValue);
        }
    }
}