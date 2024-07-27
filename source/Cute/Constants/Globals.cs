using Cute.Services;
using Spectre.Console;

namespace Cute.Constants;

internal static class Globals
{
    public const string AppName = "cute";
    public const string AppLongName = "Contentful Update Tool & Extractor";
    public const string AppDescription = "Upload and download Contentful content to Excel/Csv/Tsv/Yaml/Json/REST. Content generator and translator (LLM).";
    public const string AppMoreInfo = "https://github.com/andresharpe/cute";
    public static readonly string AppVersion = VersionChecker.GetInstalledCliVersion();

    public static readonly Style StyleHeading = new(Color.White, null, Decoration.Bold);
    public static readonly Style StyleSubHeading = new(Color.MistyRose3, null, Decoration.Bold);
    public static readonly Style StyleNormal = new(Color.LightSkyBlue3);
    public static readonly Style StyleDim = new(Color.LightPink4);
    public static readonly Style StyleAlert = new(Color.DarkOrange);
    public static readonly Style StyleAlertAccent = new(Color.Yellow4_1, null, Decoration.Bold);
}