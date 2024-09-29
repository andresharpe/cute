using Cute.Commands;

namespace Cute.Services.CliCommandInfo;

public static class CliCommandInfoExtractor
{
    public static string GetXmlCommandInfo()
    {
        using var xmlWriter = new StringWriter();

        var console = new StringWriterConsole(xmlWriter);

        var app = new CommandAppBuilder([]).Build(config => config.Settings.Console = console);

        var exitCode = app.Run(["cli", "xmldoc"]);

        var xmlOutput = xmlWriter.ToString();

        return xmlOutput;
    }
}