namespace Cute.Lib.Extensions;

public static class EnumExtensions
{
    public static string EnumNames(this Type enumType)
    {
        if (!enumType.IsEnum)
            throw new ArgumentException("Type must be an enum.");

        return string.Join(", ", Enum.GetNames(enumType));
    }
}