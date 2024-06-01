using Cut.Constants;
using Spectre.Console;

namespace Cut.Services;

public class ConsoleWriter : IConsoleWriter
{
    private readonly IAnsiConsole _console;

    public ConsoleWriter(IAnsiConsole console)
    {
        _console = console;
    }

    public void WriteHeading(string text)
    {
        _console.WriteLine(text, Globals.StyleHeading);
    }

    public void WriteDim(string text)
    {
        _console.WriteLine(text, Globals.StyleDim);
    }

    public void WriteAlert(string text)
    {
        _console.WriteLine(text, Globals.StyleAlert);
    }

    public void WriteAlertAccent(string text)
    {
        _console.WriteLine(text, Globals.StyleAlertAccent);
    }

    public void WriteRuler()
    {
        _console.Write(new Rule());
    }

    public void WriteBlankLine()
    {
        _console.WriteLine();
    }

    public T Prompt<T>(IPrompt<T> prompt)
    {
        return _console.Prompt(prompt);
    }

    public void WriteNormal(string text)
    {
        _console.WriteLine(text);
    }
}