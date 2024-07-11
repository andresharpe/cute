using System.Text.RegularExpressions;

namespace Cute.Lib.Extensions;

internal static class StringExtensions
{
    public static string CamelToPascalCase(this string value)
    {
        return Char.ToUpperInvariant(value[0]) + value[1..];
    }

    public static string ToSlug(this string phrase)
    {
        string str = phrase.RemoveAccent().ToLower();
        // invalid chars
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        // convert multiple spaces into one space
        str = Regex.Replace(str, @"\s+", " ").Trim();
        // cut and trim
        str = str.Substring(0, str.Length <= 45 ? str.Length : 45).Trim();
        str = Regex.Replace(str, @"\s", "-"); // hyphens
        return str;
    }

    private static string RemoveAccent(this string txt)
    {
        byte[] bytes = System.Text.Encoding.GetEncoding("Cyrillic").GetBytes(txt);
        return System.Text.Encoding.ASCII.GetString(bytes);
    }
}