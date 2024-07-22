using System.Text.RegularExpressions;

namespace Cute.Lib.Extensions;

internal static partial class StringExtensions
{
    public static string CamelToPascalCase(this string value)
    {
        return Char.ToUpperInvariant(value[0]) + value[1..];
    }

    public static string ToSlug(this string phrase)
    {
        string str = phrase.RemoveAccent().ToLower();
        // invalid chars
        str = InvalidChars().Replace(str, "");
        // convert multiple spaces into one space
        str = MultiWhiteSpace().Replace(str, " ").Trim();
        // cut and trim
        str = str.Substring(0, str.Length <= 45 ? str.Length : 45).Trim();
        str = WhiteSpace().Replace(str, "-"); // hyphens
        return str;
    }

    private static string RemoveAccent(this string txt)
    {
        byte[] bytes = System.Text.Encoding.GetEncoding("Cyrillic").GetBytes(txt);
        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhiteSpace();

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex InvalidChars();

    [GeneratedRegex(@"\s")]
    private static partial Regex WhiteSpace();
}