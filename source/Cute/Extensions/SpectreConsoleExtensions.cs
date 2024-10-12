namespace Cute.Extensions;

public static class SpectreConsoleExtensions
{
    public static System.Drawing.Color ToSystemDrawingColor(this Spectre.Console.Color color)
    {
        return System.Drawing.Color.FromArgb(color.R, color.G, color.B);
    }
}