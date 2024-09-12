using Cute.Lib.Enums;
using Newtonsoft.Json.Linq;

namespace Cute.Lib.Contentful.BulkActions.Models;

public class DataFilter(string FieldName, ComparisonOperation Operator, string FieldValue)
{
    public bool Compare(JObject obj)
    {
        var objValue = obj[FieldName]?.ToString();

        if (objValue == null) return Operator == ComparisonOperation.IsNull;

        return Operator switch
        {
            ComparisonOperation.Equals => objValue.Equals(FieldValue),
            ComparisonOperation.Contains => objValue.Contains(FieldValue),
            ComparisonOperation.IsNull => false,
            ComparisonOperation.NotEquals => !objValue.Equals(FieldValue),
            ComparisonOperation.NotContains => !objValue.Contains(FieldValue),
            ComparisonOperation.NotIsNull => true,
            _ => throw new NotImplementedException(),
        };
    }
}