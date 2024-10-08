﻿using Cute.Commands;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Cute.Unit.Tests;

public class XmlToMarkdownTests()
{
    [Fact]
    public void CliXmlDoc_To_Markdown_Test()
    {
        // Arrange
        // Use StringWriter to capture the XML output
        using var xmlWriter = new StringWriter();

        var console = new StringWriterConsole(xmlWriter);

        var app = new CommandAppBuilder([]).Build(config => config.Settings.Console = console);

        // Act
        // Invoke the 'cli xmldoc' command
        var exitCode = app.Run(["cli", "xmldoc"]);

        // Assert
        Assert.Equal(0, exitCode);

        // Parse the XML output
        var xmlOutput = xmlWriter.ToString();
        XDocument xmlDoc = XDocument.Parse(xmlOutput);

        // Process the XML and generate Markdown
        var markdownOutput = ProcessXmlToMarkdown(xmlDoc);

        // Optionally, perform assertions on the markdownOutput
        Assert.False(string.IsNullOrWhiteSpace(markdownOutput), "Markdown output should not be empty.");

        // Output the markdown to the test output for verification
        File.WriteAllText("../../../../../docs/CUTE-USAGE.md", markdownOutput);
    }

    // Include the ProcessXmlToMarkdown and other helper methods here
    private static string ProcessXmlToMarkdown(XDocument xmlDoc)
    {
        using var markdownWriter = new StringWriter();

        markdownWriter.WriteLine("# Cute CLI Usage\n");

        // Start processing from the root commands
        var rootCommands = xmlDoc.Element("Model")?.Elements("Command");
        if (rootCommands != null)
        {
            foreach (var command in rootCommands)
            {
                ProcessCommand(command, markdownWriter, "", 0);
            }
        }
        else
        {
            throw new Exception("No commands found in the XML.");
        }

        return markdownWriter.ToString();
    }

    private static void ProcessCommand(XElement commandNode, StringWriter writer, string commandPath, int level)
    {
        // Get current command name
        var currentCommandName = commandNode.Attribute("Name")?.Value ?? "";

        // Build full command path
        var fullCommandPath = string.IsNullOrEmpty(commandPath) ? currentCommandName : $"{commandPath} {currentCommandName}";

        // Set command heading level
        var headingLevel = new string('#', level + 2);

        // Get command description
        var commandDescription = GetDescriptionText(commandNode.Element("Description"));

        // Output command name and description
        writer.WriteLine($"{headingLevel} cute {fullCommandPath}\n");
        if (!string.IsNullOrEmpty(commandDescription))
        {
            writer.WriteLine($"{commandDescription}\n");
        }

        // Process parameters
        var parametersNode = commandNode.Element("Parameters");
        if (parametersNode != null && parametersNode.Elements("Option").Any())
        {
            writer.WriteLine($"{headingLevel}# Parameters\n");
            // Output table header
            writer.WriteLine("| Option | Description |");
            writer.WriteLine("|--------|-------------|");

            foreach (var param in parametersNode.Elements("Option"))
            {
                var shortName = param.Attribute("Short")?.Value;
                var longName = param.Attribute("Long")?.Value;
                var value = param.Attribute("Value")?.Value;
                var required = param.Attribute("Required")?.Value;

                // Get parameter description
                var paramDescription = GetDescriptionText(param.Element("Description"));

                var optionSyntax = "";
                if (!string.IsNullOrEmpty(longName))
                {
                    optionSyntax += $"--{longName}";
                }
                if (!string.IsNullOrEmpty(shortName))
                {
                    if (!string.IsNullOrEmpty(optionSyntax))
                    {
                        optionSyntax += ", ";
                    }
                    optionSyntax += $"-{shortName}";
                }
                if (!string.IsNullOrEmpty(value) && value != "NULL")
                {
                    optionSyntax += $" <{value}>";
                }

                var requiredText = required == "true" ? "Yes" : "No";

                // Escape pipes and line breaks in descriptions
                paramDescription = paramDescription.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");

                writer.WriteLine($"| {optionSyntax} | {paramDescription} |");
            }
            writer.WriteLine("");
        }

        // Process subcommands
        var subCommands = commandNode.Elements("Command");
        if (subCommands != null && subCommands.Any())
        {
            foreach (var subcommand in subCommands)
            {
                ProcessCommand(subcommand, writer, fullCommandPath, level + 1);
            }
        }
    }

    private static string GetDescriptionText(XElement? descriptionNode)
    {
        if (descriptionNode == null)
            return "";

        // Get all text nodes within the Description element
        var textNodes = descriptionNode.DescendantNodes().OfType<XText>();
        if (textNodes != null && textNodes.Any())
        {
            var text = string.Concat(textNodes.Select(t => t.Value));
            var cleanText = CleanDescription(text);
            return cleanText.Trim();
        }
        else
        {
            var text = descriptionNode.Value;
            var cleanText = CleanDescription(text);
            return cleanText.Trim();
        }
    }

    private static readonly Regex _spectreMarkup = new(@"\[(\/?)(?!\*)[^\]]+\]", RegexOptions.Compiled);

    private static string CleanDescription(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Remove color specifications like [LightSkyBlue3]
        text = _spectreMarkup.Replace(text, "");

        return text.Trim();
    }

    // Custom IAnsiConsole implementation
    public class StringWriterConsole(StringWriter writer) : IAnsiConsole
    {
        private readonly StringWriter _writer = writer;

        public IAnsiConsoleCursor Cursor { get; } = null!;

        public IAnsiConsoleInput Input { get; } = null!;

        public RenderPipeline Pipeline { get; } = null!;

        public IExclusivityMode ExclusivityMode => null!;

        public Profile Profile => throw new NotImplementedException();

        public void Clear(bool home) => throw new NotImplementedException();

        public void Write(Segment segment)
        {
            _writer.Write(segment.Text);
        }

        public void WriteLine()
        {
            _writer.WriteLine();
        }

        public void Write(IRenderable renderable)
        {
            var segments = renderable.Render(new RenderOptions(null!, new Size(1024, 1024)), 1024);

            // Write the segments
            foreach (var segment in segments)
            {
                Write(segment);
            }
        }
    }
}