using Cute.Services.Markdown.Console.Renderers.CharacterSets;

namespace Cute.Services.Markdown.Console.Formatters;

public class NumberFormatter
{
    public string Format(int number, CharacterSet characterSet)
    {
        // TODO: There is no fallback if the users font does not support nerd fonts.
        // Detecting support could be very hard.
        // Nerd font may need to be a opt in/out feature.
        return number.ToString()
            .Replace("0", characterSet.Zero)
            .Replace("1", characterSet.One)
            .Replace("2", characterSet.Two)
            .Replace("3", characterSet.Three)
            .Replace("4", characterSet.Four)
            .Replace("5", characterSet.Five)
            .Replace("6", characterSet.Six)
            .Replace("7", characterSet.Seven)
            .Replace("8", characterSet.Eight)
            .Replace("9", characterSet.Nine);
    }
}
