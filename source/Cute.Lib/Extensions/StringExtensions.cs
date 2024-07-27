using System.Text.RegularExpressions;

namespace Cute.Lib.Extensions;

public static partial class StringExtensions
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
        str = str[..(str.Length <= 45 ? str.Length : 45)].Trim();
        str = WhiteSpace().Replace(str, "-"); // hyphens
        return str;
    }

    private static string RemoveAccent(this string text)
    {
        byte[] bytes = System.Text.Encoding.GetEncoding("Cyrillic").GetBytes(text);
        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    public static string RemoveEmojis(this string text)
    {
        return EmojiAndOtherUnicode().Replace(text, "");
    }

    public static string Snip(this string text, int snipTo)
    {
        if (text.Length <= snipTo) return text;

        return text[..(snipTo - 1)] + "..";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhiteSpace();

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex InvalidChars();

    [GeneratedRegex(@"\s")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"[\u0000-\u0008\u000A-\u001F\u0100-\uFFFF]")]
    private static partial Regex EmojiAndOtherUnicode();
}