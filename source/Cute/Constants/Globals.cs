using Cute.Services;
using Spectre.Console;

namespace Cute.Constants;

public static class Globals
{
    public const string AppName = "cute";
    public const string AppLongName = "Contentful Update Tool & Extractor";
    public const string AppDescription = "Bulk download/upload/edit/publish/unpublish and delete Contentful content from and to Excel/Csv/Tsv/Yaml/Json/REST. A.I. content generator and translator.";
    public const string AppMoreInfo = "https://github.com/andresharpe/cute";
    public static readonly string AppVersion = VersionChecker.GetInstalledCliVersion();

    public static readonly Style StyleHeading = new(Color.White, null, Decoration.Bold);
    public static readonly Style StyleSubHeading = new(Color.MistyRose3, null, Decoration.Bold);
    public static readonly Style StyleNormal = new(Color.LightSkyBlue3);
    public static readonly Style StyleDim = new(Color.LightPink4);
    public static readonly Style StyleAlert = new(Color.DarkOrange);
    public static readonly Style StyleAlertAccent = new(Color.Yellow4_1, null, Decoration.Bold);
    public static readonly Style StyleCode = new(Color.White, new Color(31, 22, 22));
    public static readonly Style StyleCodeHeading = new(Color.White, new Color(20, 14, 14));
    public static readonly Style StyleInput = new(new Color(192, 192, 192), new Color(40, 40, 40));
}