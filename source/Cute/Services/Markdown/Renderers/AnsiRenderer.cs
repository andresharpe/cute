using Cute.Services.Markdown.Console.Formatters;
using Cute.Services.Markdown.Console.Options;
using Cute.Services.Markdown.Console.Parsers;
using Cute.Services.Markdown.Console.Renderers.CharacterSets;
using Cute.Services.Markdown.Console.SyntaxHighlighters;
using Markdig.Syntax;
using Spectre.Console;
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
            switch (block)
            {
                case HeadingBlock headingBlock:
                    WriteHeadingBlock(headingBlock);
                    break;

                case ParagraphBlock paragraphBlock:
                    WriteParagraphBlock(paragraphBlock);
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

            _console.WriteLine();
        }
    }

    private int GetConsoleWidth()
    {
        return _console.Profile.Width;
    }

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