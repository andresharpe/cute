using Cute.Constants;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Cute.Services;

public class ConsoleWriter : IConsoleWriter
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
}