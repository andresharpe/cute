using Contentful.Core.Models;
using Cute.Lib.Contentful;

namespace Cute.Lib.InputAdapters.EntryAdapters;

public class FindAndReplaceFieldsInputAdapter(string locale,
    ContentLocales contentLocales, string[] fields, string[] findValues, string[] replaceValues,
    ContentType contentType, ContentfulConnection contentfulConnection)

    : FieldsInputAdapterBase(nameof(FindAndReplaceFieldsInputAdapter),
        locale, contentLocales, fields, findValues, replaceValues, contentType, contentfulConnection)
{
    protected override void CompareAndEdit(Dictionary<string, object?> newFlatEntry, string fieldName, string? fieldFindValue, string? fieldReplaceValue, string? oldFieldValue)
    {
        if (oldFieldValue == null || fieldFindValue == null) return;

        if (oldFieldValue.Contains(fieldFindValue))
        {
            newFlatEntry.Add(fieldName, oldFieldValue.Replace(fieldFindValue, fieldReplaceValue ?? string.Empty));
        }
    }
}