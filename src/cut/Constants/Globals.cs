using Spectre.Console;

namespace Cut.Constants;

internal static class Globals
{
    public const string AppName = "cut";
    public const string AppLongName = "Contentful Update Tool";
    public const string AppPurpose = "Bulk upload and download of Contentful content to and from excel/csv/tsv/yaml/json/sql.";

    public static readonly Style StyleHeading = new(Color.White, null, Decoration.Bold);
    public static readonly Style StyleDim = new(Color.Grey);
    public static readonly Style StyleAlert = new(Color.DarkOrange);
    public static readonly Style StyleAlertAccent = new(Color.CadetBlue, null, Decoration.Bold);
}