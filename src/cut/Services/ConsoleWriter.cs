
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
    public void WriteBody(string text)
    {
        _console.WriteLine(text, Globals.StyleBody);
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
}
