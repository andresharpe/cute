using Cute.Constants;
using Cute.Lib.Extensions;
using Cute.Services.Markdown.Console.Options;
using Markdig.Syntax;
using Spectre.Console;
using System.Text;
using System.Text.RegularExpressions;

namespace Cute.Services.Markdown.Console.Renderers;

public partial class AnsiRenderer
{
    private void WriteFencedCodeBlock(FencedCodeBlock block)
    {
        var indentation = GetIndentation();

        var code = string.Join("\n", block.Lines.Lines).TrimEnd();

        var lang = block.Info;

        var highlightedCode = _syntaxHighlighter
            .GetHighlightedSyntax(code, lang, FeatureFlags.ForceBasicSyntaxHighlighter);

        var consoleWidth = GetConsoleWidth();

        var blankLine = new string(' ', consoleWidth - indentation.Length);

        var blankLineIndent = indentation;

        using var buffer = new StringWriter();

        var subRenderer = new AnsiRendererBuilder()
            .RedirectOutput(buffer)
            .Build();

        var sbMarkedUpBlock = new StringBuilder();

        foreach (var line in highlightedCode)
        {
            sbMarkedUpBlock.Append(line.RemoveNewLines() + '\n');
        }

        subRenderer._console.Write(new Markup(sbMarkedUpBlock.ToString()));

        var plainTextLines = AnsiConsoleToTextOnly(buffer.ToString())
            .Split('\n')
            .Reverse()
            .Skip(1)
            .Reverse()
            .ToArray();

        var table = new Table()
            .HideHeaders()
            .HideRowSeparators()
            .Border(TableBorder.None)
            .AddColumn(new TableColumn(string.Empty).Padding(0, 0, 0, 0))
            .AddColumn(new TableColumn(string.Empty).Padding(0, 0, 0, 0))
            .Expand();

        var background = $"default on #{Globals.StyleCode.Background.ToHex().ToLower()}";

        var lineNumber = 0;

        var sb = new StringBuilder();

        sb.AppendLine($"[{background}]{blankLine}[/]");

        foreach (var markupText in highlightedCode)
        {
            var plainText = plainTextLines[lineNumber];
            var plainTextLength = plainText.Length;
            var paddingLength = Math.Max(consoleWidth - plainTextLength - indentation.Length + 1, 0);
            var paddedMarkupText = $"[{background}]{markupText}{new string(' ', paddingLength)}[/]";
            sb.AppendLine(paddedMarkupText.RemoveNewLines());
            lineNumber++;
        }

        sb.AppendLine($"[{background}]{blankLine}[/]");

        var codeBlock = sb.ToString();

        table.AddRow(new Text(blankLineIndent), new Markup(codeBlock));

        _console.Write(table);
    }
}