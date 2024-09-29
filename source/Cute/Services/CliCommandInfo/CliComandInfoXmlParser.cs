using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Cute.Services.CliCommandInfo;

public static partial class CliCommandInfoXmlParser
{
    public static CliCommandInfo FromXml(string xml, string rootCommandDescription)
    {
        var xmlDoc = XDocument.Parse(xml);

        var commandBuilder = new CliCommandInfo.Builder()
            .WithName(string.Empty)
            .WithDescription(rootCommandDescription);

        var rootCommands = xmlDoc.Element("Model")?.Elements("Command")
            ?? throw new Exception("No commands found in the XML.");

        foreach (var command in rootCommands)
        {
            commandBuilder.AddSubCommand(ParseCommand(command));
        }

        var rootCommand = commandBuilder.Build();

        rootCommand.ConsolidateOptions();

        return rootCommand;
    }

    private static CliCommandInfo ParseCommand(XElement element)
    {
        var name = element.Attribute("Name")?.Value.ToString()
            ?? throw new Exception("Command name is required and not in XML docs.");

        var description = element.Element("Description")?.Value.ToString()
            ?? throw new Exception("Command description is required and not in XML docs.");

        var parameters = element.Element("Parameters");

        var builder = new CliCommandInfo.Builder()
            .WithName(name)
            .WithDescription(RemoveMarkup(description));

        foreach (var optionElement in parameters?.Elements("Option") ?? [])
        {
            var shortOptionName = optionElement.Attribute("Short")?.Value.ToString()
                ?? throw new Exception("Option short option name is required and not in XML docs.");

            var longOptionName = optionElement.Attribute("Long")?.Value.ToString()
                ?? throw new Exception("Option long option name is required and not in XML docs.");

            var optionDescription = optionElement.Element("Description")?.Value.ToString()
                ?? throw new Exception("Option description is required and not in XML docs.");

            builder.AddOption(
                shortOptionName,
                longOptionName,
                RemoveMarkup(optionDescription)
            );
        }

        foreach (var subCommandElement in element.Elements("Command"))
        {
            var subCommand = ParseCommand(subCommandElement);
            builder.AddSubCommand(subCommand);
        }

        return builder.Build();
    }

    private static string RemoveMarkup(string input)
    {
        if (input.AsSpan().IndexOf('[') == -1) return input;

        return RemoveMarkupRegex().Replace(input, string.Empty);
    }

    [GeneratedRegex(@"\[.*?\]")]
    private static partial Regex RemoveMarkupRegex();
}