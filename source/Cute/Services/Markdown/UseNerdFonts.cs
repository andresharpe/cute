namespace Cute.Services.Markdown;

/// <summary>
/// <see href="https://www.nerdfonts.com/">Nerd Fonts</see>
/// provide additional glyphs that help us render beautiful output in the terminal.  However not all
/// fonts supports these characters.
/// </summary>
public enum UseNerdFonts
{
    /// <summary>
    /// Use Nerd Fonts to render special characters, like list bullets.
    /// </summary>
    Yes,

    /// <summary>
    /// Use glyphs from the standard and extended ASCII table to render special characters, like list bullets.
    /// </summary>
    No
}