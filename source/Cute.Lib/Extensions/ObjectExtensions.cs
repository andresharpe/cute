using System.Globalization;

namespace Cute.Lib.Extensions;

public static class ObjectExtensions
{
    public static DateTime FromInvariantDateTime(this object value)
    {
        if (value is DateTime dt) return dt;

        if (value is DateTimeOffset dto) return dto.DateTime;

        if (value is string dtString) return Convert.ToDateTime(dtString, CultureInfo.InvariantCulture);

        return Convert.ToDateTime(value);
    }

    public static IReadOnlyDictionary<string, object?> ToReadOnlyDictionary(this object obj)
    {
        return obj
            .GetType()
            .GetProperties()
            .ToDictionary(p => p.Name, p => p.GetValue(obj));
    }
}