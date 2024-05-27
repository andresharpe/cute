using Spectre.Console;

namespace Cut.Constants;

internal static class Globals
{

    public static readonly Style StyleHeading = new (Color.White, null, Decoration.Bold);
    public static readonly Style StyleBody = new (Color.Grey);
    public static readonly Style StyleAlert = new (Color.DarkOrange);
    public static readonly Style StyleAlertAccent = new(Color.CadetBlue, null, Decoration.Bold);
}
