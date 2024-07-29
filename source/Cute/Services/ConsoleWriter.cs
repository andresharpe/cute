using Cute.Constants;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;
using System.Text.RegularExpressions;

namespace Cute.Services;

public partial class ConsoleWriter : IConsoleWriter
{
    public static bool EnableConsole { get; set; } = true;

    private readonly IAnsiConsole? _defaultConsole;

    public ILogger? Logger { get; set; }

    private IAnsiConsole? Console => EnableConsole ? _defaultConsole : null;

    public ConsoleWriter(IAnsiConsole console)
    {
        if (EnableConsole)
        {
            _defaultConsole = console;
        }
    }

    public void WriteHeading(string text)
    {
        Console?.WriteLine(text, Globals.StyleHeading);
        Logger?.LogInformation(message: text);
    }

    public void WriteHeading(string textTemplate, params object?[] args)
    {
        Console?.MarkupLine(Format(textTemplate, Globals.StyleHeading, Globals.StyleAlertAccent, args));
        Logger?.LogInformation(message: textTemplate, args: args);
    }

    public void WriteHeadingWithHighlights(FormattableString text, Style highlightStyle)
    {
        Console?.MarkupLine(Format(text.Format, Globals.StyleHeading, highlightStyle, text.GetArguments()));
        Logger?.LogInformation(text.Format, text.GetArguments());
    }

    private static string Format(string textTemplate, Style style, Style highlightStyle, params object?[] args)
    {
        var matches = ParameterMatch().Matches(textTemplate);
        var sb = new StringBuilder(textTemplate);
        var i = 0;
        foreach (Match m in matches)
        {
            if (i > args.Length - 1) break;
            sb.Replace(m.Value, $"[{highlightStyle.Foreground}]{args[i++]?.ToString()}[/]");
        }
        sb.Insert(0, $"[{style.Foreground}]");
        sb.Append("[/]");
        return sb.ToString();
    }

    public void WriteSubHeading(string text)
    {
        Console?.WriteLine(text, Globals.StyleSubHeading);
        Logger?.LogInformation(message: text);
    }

    public void WriteDim(string text)
    {
        Console?.WriteLine(text, Globals.StyleDim);
        Logger?.LogInformation(message: text);
    }

    public void WriteAlert(string text)
    {
        Console?.WriteLine(text, Globals.StyleAlert);
        Logger?.LogInformation(message: text);
    }

    public void WriteAlertAccent(string text)
    {
        Console?.WriteLine(text, Globals.StyleAlertAccent);
        Logger?.LogInformation(message: text);
    }

    public void WriteRuler()
    {
        Console?.Write(new Rule() { Style = Globals.StyleDim });
    }

    public void WriteBlankLine()
    {
        Console?.WriteLine();
    }

    public T Prompt<T>(IPrompt<T> prompt)
    {
        return _defaultConsole!.Prompt(prompt);
    }

    public void WriteNormal(string text)
    {
        Console?.WriteLine(text, Globals.StyleNormal);
        Logger?.LogInformation(message: text);
    }

    public void WriteNormal(string textTemplate, params object?[] args)
    {
        Console?.MarkupLine(Format(textTemplate, Globals.StyleNormal, Globals.StyleHeading, args));
        Logger?.LogInformation(message: textTemplate, args: args);
    }

    public void WriteNormalWithHighlights(FormattableString text, Style highlightStyle)
    {
        Console?.MarkupLine(Format(text.Format, Globals.StyleNormal, highlightStyle, text.GetArguments()));
        Logger?.LogInformation(text.Format, text.GetArguments());
    }

    public void WriteLine()
    {
        Console?.WriteLine();
    }

    public void Write(Renderable renderable)
    {
        Console?.Write(renderable);
    }

    public void WriteLine(string text, Style style)
    {
        Console?.WriteLine(text, style);
        Logger?.LogInformation(message: text);
    }

    public void WriteException(Exception ex, ExceptionSettings settings)
    {
        Console?.WriteException(ex, settings);
        Logger?.LogError(ex, "An error occured.");
    }

    [GeneratedRegex(@"\{(?<parameter>\w+)\}")]
    private static partial Regex ParameterMatch();
}