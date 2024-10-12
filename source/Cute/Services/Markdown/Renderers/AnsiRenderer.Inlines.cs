using Cute.Constants;
using Cute.Lib.Extensions;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;
using Spectre.Console;
using System.Text;

namespace Cute.Services.Markdown.Console.Renderers;

public partial class AnsiRenderer
{
    private bool _isQuote = false;

    private void WriteInlines(IEnumerable<Inline> inlines,
        string? markupTag = null,
        bool indentFirstLine = true)
    {
        var linesWritten = 0;

        foreach (var inline in inlines)
        {
            var indentIt = indentFirstLine || linesWritten > 0;

            linesWritten = 0;

            switch (inline)
            {
                case LiteralInline literal:
                    WriteLiteralInline(literal.ToString(), markupTag, indentIt);
                    break;

                case EmphasisInline emphasis:
                    WriteEmphasisInline(emphasis, markupTag, indentIt);
                    break;

                case CodeInline code:
                    WriteCodeInline(code);
                    break;

                case LinkInline link:
                    WriteInlineLink(link);
                    break;

                case AutolinkInline link:
                    WriteAutoInlineLink(link);
                    break;

                case LineBreakInline:
                    if (_isQuote)
                    {
                        var _quoteLinePrefix = $"[{_accentColor}] {_characterSet.QuotePrefix} [/]";
                        _console.Markup($"\n{_quoteLinePrefix}");
                        break;
                    }
                    _console.WriteLine();
                    linesWritten = 1;
                    break;

                case TaskList task:
                    var bullet = task.Checked ? _characterSet.TaskListBulletDone : _characterSet.TaskListBulletToDo;
                    _console.Markup($"[{_accentColor}]{bullet.EscapeMarkup()}[/]");
                    break;

                default:
                    // We shouldn't be able to get here.
                    ThrowOrFallbackToPlainText(inline);
                    break;
            }
        }
    }

    private static readonly string _defaultColor = Globals.StyleSubHeading.Foreground.ToString();
    private static readonly string _highlightedColor = Globals.StyleHeading.Foreground.ToString();
    private static readonly string _accentColor = Globals.StyleAlertAccent.Foreground.ToString();
    private static readonly string _accentColor2 = Globals.StyleAlert.Foreground.ToString();
    private static readonly string _dimColor = Globals.StyleDim.Foreground.ToString();

    private int WriteLiteralInline(string content, string? markupTag = null,
        bool indentFirstLine = true)
    {
        var linesWritten = 0;

        var displayContent = content.EscapeMarkup();

        var indentation = GetIndentation();

        var firstLineIndentation = indentFirstLine
            ? indentation
            : string.Empty;

        var currentLine = 1;

        var maxLineWidth = GetMaxWidth();

        var currentConsoleColumn = GetConsoleColumn();

        var maxchars = maxLineWidth - indentation.Length;

        var firstLineMaxchars = maxLineWidth - indentation.Length - currentConsoleColumn;

        if (firstLineMaxchars < 0)
        {
            firstLineMaxchars = maxLineWidth;

            _console.WriteLine();

            linesWritten++;

            firstLineIndentation = indentation;
        }

        foreach (var line in displayContent.AsSpan().GetFixedLines(maxchars, firstLineMaxchars))
        {
            var loopIndentation = currentLine == 1
                ? firstLineIndentation
                : indentation;

            var displayLine = markupTag is null
                ? $"{loopIndentation}[{_defaultColor}]{line}[/]"
                : $"{loopIndentation}[{markupTag.Trim()}]{line}[/]";

            if (currentLine > 1)
            {
                _console.WriteLine();
                linesWritten++;
            }

            _console.Markup(displayLine);
            currentLine++;
        }

        return linesWritten;
    }

    private void WriteEmphasisInline(EmphasisInline emphasis, string? markupTag = null, bool indentFirstLine = true)
    {
        switch (emphasis.DelimiterChar)
        {
            case '*':
                markupTag += $"bold {_highlightedColor}";
                WriteInlines(emphasis, markupTag, indentFirstLine: indentFirstLine);
                break;

            case '_':
                markupTag += $"italic {_highlightedColor}";
                WriteInlines(emphasis, markupTag, indentFirstLine: indentFirstLine);
                break;

            case '~':
                markupTag += $"strikethrough {_highlightedColor}";
                WriteInlines(emphasis, markupTag, indentFirstLine: indentFirstLine);
                break;

            default:
                // We shouldn't be able to get here.
                // All cases of emphasis should be handled above.
                var exceptionMessage = $"Unsupported emphasis delimited found: {emphasis.DelimiterChar}.";
                var fallbackText = emphasis?.ToString() ?? string.Empty;
                ThrowOrFallbackToPlainText(exceptionMessage, fallbackText);
                break;
        }
    }

    private void WriteCodeInline(CodeInline code)
    {
        var sb = new StringBuilder();

        sb.Append($"[{_defaultColor}]");
        sb.Append(_characterSet.InlineCodeOpening);
        sb.Append("[italic]");
        sb.Append(code.Content.EscapeMarkup());
        sb.Append("[/]");
        sb.Append(_characterSet.InlineCodeClosing);
        sb.Append("[/]");

        _console.Markup(sb.ToString());
    }
}