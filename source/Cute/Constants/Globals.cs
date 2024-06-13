using Spectre.Console;

namespace Cute.Constants;

internal static class Globals
{
    public const string AppName = "cut";
    public const string AppLongName = "Contentful Update Tool";
    public const string AppDescription = "Bulk upload and download of Contentful content (excel/csv/tsv/yaml/json/sql).";
    public const string AppMoreInfo = "https://github.com/andresharpe/cut";

    public static readonly Style StyleHeading = new(Color.White, null, Decoration.Bold);
    public static readonly Style StyleSubHeading = new(Color.MistyRose3, null, Decoration.Bold);
    public static readonly Style StyleNormal = new(Color.LightSkyBlue3);
    public static readonly Style StyleDim = new(Color.LightPink4);
    public static readonly Style StyleAlert = new(Color.DarkOrange);
    public static readonly Style StyleAlertAccent = new(Color.Yellow4_1, null, Decoration.Bold);
}