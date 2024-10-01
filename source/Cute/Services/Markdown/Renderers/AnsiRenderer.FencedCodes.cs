using Cute.Constants;
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
        var code = string.Join("\n", block.Lines.Lines).TrimEnd();
        var lang = block.Info;
        var highlightedCode = _syntaxHighlighter.GetHighlightedSyntax(code, lang, FeatureFlags.ForceBasicSyntaxHighlighter);

        var consoleWidth = System.Console.WindowWidth - 2;

        var blankLine = new string(' ', consoleWidth);

        using var buffer = new StringWriter();

        var subRenderer = new AnsiRendererBuilder()
            .RedirectOutput(buffer)
            .Build();

        var sbMarkedUpBlock = new StringBuilder();
        foreach (var line in highlightedCode)
        {
            sbMarkedUpBlock.Append(line + '\n');
        }

        subRenderer._console.Write(new Markup(sbMarkedUpBlock.ToString()));

        var plainTextLines = AnsiConsoleToTextOnly(buffer.ToString()).Split('\n');

        var table = new Table()
            .HideHeaders()
            .AddColumn(new TableColumn(string.Empty))
            .Expand()
            .Border(TableBorder.None);

        var background = $"default on #{Globals.StyleCode.Background.ToHex().ToLower()}";

        var lineNumber = 0;
        var sb = new StringBuilder();
        sb.AppendLine($"[{background}]{blankLine}[/]");
        foreach (var markupText in highlightedCode)
        {
            var plainText = plainTextLines[lineNumber];
            var plainTextLength = plainText.Length;
            var paddingLength = consoleWidth - plainTextLength;
            var paddedMarkupText = $"[{background}]{markupText}{new string(' ', paddingLength)}[/]";
            sb.AppendLine(paddedMarkupText);
            lineNumber++;
        }
        sb.AppendLine($"[{background}]{blankLine}[/]");

        var codeBlock = sb.ToString();
        table.AddRow(new Markup(codeBlock));

        _console.Write(table);
    }

    private static string StripMarkupTags(string markupText)
    {
        string tempText = markupText.Replace("[[", "ESCAPED_LEFT_BRACKET").Replace("]]", "ESCAPED_RIGHT_BRACKET");

        string strippedText = SpectreConsoleMarkupRegex().Replace(tempText, string.Empty);

        strippedText = strippedText.Replace("ESCAPED_LEFT_BRACKET", "[").Replace("ESCAPED_RIGHT_BRACKET", "]");

        return strippedText;
    }

    [GeneratedRegex(@"\[\/?(?:\w+|\#\w+|[^\]]+)\]")]
    private static partial Regex SpectreConsoleMarkupRegex();
}