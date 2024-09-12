using System.ComponentModel;
using System.Globalization;

namespace Cute.TypeConverters;

public class PartialStringToEnumConverter<TEnum> : TypeConverter where TEnum : struct, Enum
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            var matchingEnum = PartialStringToEnumConverter<TEnum>.GetMatchingEnum(stringValue);

            return matchingEnum == null
                ? throw new InvalidOperationException($"No unique match found for '{stringValue}' in {typeof(TEnum).Name}")
                : (object)matchingEnum.Value;
        }

        return base.ConvertFrom(context, culture, value)!;
    }

    private static TEnum? GetMatchingEnum(string input)
    {
        var matchingValues = Enum.GetValues(typeof(TEnum)).Cast<TEnum>()
            .Where(e => Enum.GetName(typeof(TEnum), e)?.StartsWith(input, StringComparison.OrdinalIgnoreCase) ?? false)
            .ToList();

        if (matchingValues.Count == 1)
        {
            return matchingValues[0];
        }

        return null;
    }
}