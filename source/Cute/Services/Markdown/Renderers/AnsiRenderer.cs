using Cute.Services.Markdown.Console.Formatters;
using Cute.Services.Markdown.Console.Options;
using Cute.Services.Markdown.Console.Parsers;
using Cute.Services.Markdown.Console.Renderers.CharacterSets;
using Cute.Services.Markdown.Console.SyntaxHighlighters;
using Markdig.Syntax;
using Spectre.Console;
using System.Text.RegularExpressions;
using MarkdownTable = Markdig.Extensions.Tables;

namespace Cute.Services.Markdown.Console.Renderers;

/// <summary>
/// Renders Markdown in the terminal, using Ansi Escape Codes to apply formatting.
/// </summary>
public partial class AnsiRenderer
{
    private readonly MarkdownParser _markdownParser;
    private readonly SyntaxHighlighter _syntaxHighlighter;
    private readonly NumberFormatter _numberFormatter;
    private readonly CharacterSet _characterSet;
    private readonly IAnsiConsole _console;
    private int _indentationLevel = 0;

    private string _markdown = string.Empty;

    internal AnsiRenderer(
        MarkdownParser markdownParser,
        SyntaxHighlighter syntaxHighlighter,
        NumberFormatter numberFormatter,
        CharacterSet characterSet,
        IAnsiConsole console)
    {
        _markdownParser = markdownParser;
        _syntaxHighlighter = syntaxHighlighter;
        _numberFormatter = numberFormatter;
        _characterSet = characterSet;
        _console = console;
    }

    public void Write(string markdown)
    {
        _markdown = markdown;
        var doc = _markdownParser.ConvertToMarkdownDocument(markdown);
        WriteBlocks(doc);
    }

    private void WriteBlocks(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            WriteBlock(block);

            _console.WriteLine();
        }
    }

    private void WriteBlock(Block block, bool indentFirstLine = true)
    {
        switch (block)
        {
            case HeadingBlock headingBlock:
                WriteHeadingBlock(headingBlock);
                break;

            case ParagraphBlock paragraphBlock:
                WriteParagraphBlock(paragraphBlock, indentFirstLine: indentFirstLine);
                break;

            case QuoteBlock quoteBlock:
                WriteQuoteBlock(quoteBlock);
                break;

            case ListBlock listBlock:
                WriteListBlock(listBlock);
                break;

            case MarkdownTable.Table tableBlock:
                WriteTableBlock(tableBlock);
                break;

            case FencedCodeBlock fencedCodeBlock:
                WriteFencedCodeBlock(fencedCodeBlock);
                break;

            case LinkReferenceDefinitionGroup linkBlock:
                WriteLinkReferenceDefinitionBlock(linkBlock);
                break;

            case ThematicBreakBlock thematicBreakBlock:
                WriteThematicBreakBlock(thematicBreakBlock);
                break;

            default:
                // We shouldn't be able to get here.
                // The case above should handle all possibilities.
                ThrowOrFallbackToPlainText(block);
                break;
        };
    }

    private void Indent()
    {
        _indentationLevel++;
    }

    private void UnIndent()
    {
        _indentationLevel--;
        if (_indentationLevel < 0) _indentationLevel = 0;
    }

    private static List<string> _indentations = [string.Empty];

    private string GetIndentation(int? level = null)
    {
        level ??= _indentationLevel;

        while (_indentations.Count - 1 < level)
        {
            _indentations.Add(new string(' ', _indentationLevel * 4));
        }
        return _indentations[level.Value];
    }

    private int GetConsoleWidth()
    {
        return _console.Profile.Width;
    }

    private int GetConsoleColumn()
    {
        return System.Console.CursorLeft;
    }

    private int GetMaxWidth()
    {
        return GetConsoleWidth() / 4 * 3;
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

    private static string AnsiConsoleToTextOnly(string input)
    {
        string cleanOutput = AnsiControlCodes().Replace(input, string.Empty);

        return cleanOutput;
    }

    [GeneratedRegex(@"\u001b\[[0-9;]*[a-zA-Z]")]
    private static partial Regex AnsiControlCodes();

    private void ThrowOrFallbackToPlainText(MarkdownObject markdownObject)
    {
        var span = markdownObject.Span;

        if (FeatureFlags.ThrowOnUnsupportedMarkdownType)
        {
            var exceptionMessage = $"Unsupported markdown type {markdownObject.GetType()} found from character {span.Start} to {span.End}: {span}.";
            throw new Exception(exceptionMessage);
        }

        // Cannot use ranges here - not support supported by NetStandard2.0.
        _console.Write($"[{_defaultColor}]{_markdown.Substring(span.Start, span.Length)}[/]");
    }

    private void ThrowOrFallbackToPlainText(string exceptionMessage, string fallbackText)
    {
        if (FeatureFlags.ThrowOnUnsupportedMarkdownType)
        {
            throw new Exception(exceptionMessage);
        }

        _console.Write(fallbackText);
    }

    private void ThrowOrFallbackToPlainText(string exceptionMessage, Markup fallbackMarkupText)
    {
        if (FeatureFlags.ThrowOnUnsupportedMarkdownType)
        {
            throw new Exception(exceptionMessage);
        }

        _console.Write(fallbackMarkupText);
    }
}