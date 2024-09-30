using Cute.Services.Markdown.Console.Converters;
using Cute.Services.Markdown.Console.Formatters;
using Cute.Services.Markdown.Console.Options;
using Cute.Services.Markdown.Console.Parsers;
using Cute.Services.Markdown.Console.Renderers.CharacterSets;
using Cute.Services.Markdown.Console.SyntaxHighlighters;
using Spectre.Console;

namespace Cute.Services.Markdown.Console.Renderers;

/// <summary>
/// Creates an AnsiRenderer.
/// </summary>
public class AnsiRendererBuilder
{
    private static readonly SyntaxHighlighter SyntaxHighlighter = new();
    private static readonly NumberFormatter NumberFormatter = new();
    private static readonly MarkdownParser Parser = new();

    private AnsiSupport _ansiSupport = AnsiSupport.Detect;
    private ColorSystemSupport _colorSystem = ColorSystemSupport.Detect;
    private CharacterSet _characterSet = new AsciiCharacterSet();
    private TextWriter? _writer;

    /// <summary>
    /// Adds a customer writer.
    /// Allows the caller to divert the renderer output.
    /// Useful for testing and debugging.
    /// </summary>
    /// <param name="writer">Ansi formatted string will be built using the supplied writer</param>
    public AnsiRendererBuilder RedirectOutput(TextWriter writer)
    {
        _writer = writer;
        return this;
    }

    /// <summary>
    /// Clone capabilities from an existing console.
    /// </summary>
    /// <param name="console">Copy configuration from this console.</param>
    public AnsiRendererBuilder InheritSettings(IAnsiConsole console)
    {
        _colorSystem = ColorSystemSupportConverter.FromColorSystem(console.Profile.Capabilities.ColorSystem);
        _ansiSupport = AnsiSupportConverter.FromAnsiSupported(console.Profile.Capabilities.Ansi);

        return this;
    }

    public  AnsiRendererBuilder SetNerdFontsUsage(UseNerdFonts useNerdFonts)
    {
        _characterSet = useNerdFonts == UseNerdFonts.Yes
            ? new NerdFontCharacterSet()
            : new AsciiCharacterSet();
        return this;
    }


    /// <summary>
    /// Builds and returns an AnsiRenderer.
    /// </summary>
    public AnsiRenderer Build()
    {
        var console = GetConsole();
        return new AnsiRenderer(Parser, SyntaxHighlighter, NumberFormatter, _characterSet, console);
    }

    private IAnsiConsole GetConsole()
    {
        if (_writer is null)
        {
            return AnsiConsole.Console;
        }

        if (FeatureFlags.ForceAnsiColour)
        {
            _ansiSupport = AnsiSupport.Yes;
            _colorSystem = ColorSystemSupport.TrueColor;
        }

        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            ColorSystem = _colorSystem,
            Ansi = _ansiSupport,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(_writer)
        });

        return console;
    }
}
