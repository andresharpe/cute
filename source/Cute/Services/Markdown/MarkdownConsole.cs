using Cute.Constants;
using Cute.Services.Markdown.Console.Renderers;

namespace Cute.Services.Markdown;

/// <summary>
/// Renders markdown in the terminal.
/// </summary>
public static class MarkdownConsole
{
    private const UseNerdFonts UseNerdFontsDefault = UseNerdFonts.No;
    private static UseNerdFonts UseNerdFontsField = UseNerdFontsDefault;
    private static AnsiRenderer DefaultRenderer = GetAnsiRenderer();

    /// <summary>
    /// Configure Nerd Fonts usage for special characters, like list bullets.
    /// <see href="https://www.nerdfonts.com/">Nerd Fonts</see> are awesome, but not supported by all fonts.
    /// </summary>
    public static UseNerdFonts UseNerdFonts
    {
        get => UseNerdFontsField;
        set
        {
            UseNerdFontsField = value;
            DefaultRenderer = GetAnsiRenderer();
        }
    }

    /// <summary>
    /// Writes formatted markdown in the console.
    /// </summary>
    /// <param name="markdown">Markdown to format.</param>
    public static void Write(string markdown)
    {
        DefaultRenderer.Write(markdown);
    }

    /// <summary>
    /// Writes formatted markdown in the console.
    /// </summary>
    /// <param name="markdown">Markdown to format.</param>
    /// <param name="writer">
    ///     Override the default console.
    ///     <remarks>
    ///     Useful for test and debugging only.
    ///     </remarks>
    /// </param>
    public static void Write(string markdown, TextWriter writer)
    {
        GetAnsiRenderer(writer).Write(markdown);
    }

    private static AnsiRenderer GetAnsiRenderer()
    {
        return new AnsiRendererBuilder()
            .SetNerdFontsUsage(UseNerdFontsField)
            .Build();
    }

    private static AnsiRenderer GetAnsiRenderer(TextWriter writer)
    {
        return new AnsiRendererBuilder()
            .SetNerdFontsUsage(UseNerdFontsField)
            .RedirectOutput(writer)
            .Build();
    }
}