using System.Text;

namespace Cute.Lib.Contentful;

public class ContentfulIdGenerator
{
    private static readonly Random _random = new();

    private const string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string NewId(string prefix = "cute-")
    {
        var result = new StringBuilder($"{prefix}-");

        for (int i = 0; i < 22; i++)
        {
            result.Append(_chars[_random.Next(_chars.Length)]);
        }

        return result.ToString();
    }
}