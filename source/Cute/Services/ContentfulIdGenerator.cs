using System.Text;

namespace Cute.Services;

internal class ContentfulIdGenerator
{
    private static readonly Random _random = new Random();

    private const string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string NewId()
    {
        var result = new StringBuilder("cut-");

        for (int i = 0; i < 22; i++)
        {
            result.Append(_chars[_random.Next(_chars.Length)]);
        }

        return result.ToString();
    }
}