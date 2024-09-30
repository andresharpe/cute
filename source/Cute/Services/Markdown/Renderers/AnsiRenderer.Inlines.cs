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
        string? markupTag = null, int indent = 0)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    WriteLiteralInline(literal.ToString(), markupTag, indent: indent);
                    break;

                case EmphasisInline emphasis:
                    WriteEmphasisInline(emphasis, markupTag);
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
                        var _quoteLinePrefix = $"[{_highlighted}] {_characterSet.QuotePrefix} [/]";
                        _console.Markup($"\n{_quoteLinePrefix}");
                        break;
                    }
                    _console.WriteLine();
                    break;

                case TaskList task:
                    var bullet = task.Checked ? _characterSet.TaskListBulletDone : _characterSet.TaskListBulletToDo;
                    _console.Markup($"[{_highlighted}]{bullet.EscapeMarkup()}[/]");
                    break;

                default:
                    // We shouldn't be able to get here.
                    ThrowOrFallbackToPlainText(inline);
                    break;
            }
        }
    }

    private static string _defaultColor = Globals.StyleSubHeading.Foreground.ToString();

    private void WriteLiteralInline(string content, string? markupTag = null, int indent = 0)
    {
        var result = content.EscapeMarkup();
        var indentation = indent == 0 ? string.Empty : new string(' ', 4 + (indent * 3));
        var currentLine = 1;
        var maxchars = 80 - indentation.Length;

        foreach (var line in result.AsSpan().GetFixedLines(maxchars))
        {
            var loopIndentation = currentLine == 1 ? string.Empty : indentation;
            if (markupTag is not null)
            {
                _console.Markup($"{loopIndentation}[{markupTag.Trim()} {_defaultColor}]{line}[/]");
                _console.WriteLine();
                continue;
            }
            if (currentLine > 1)
            {
                _console.WriteLine();
            }
            _console.Markup($"{loopIndentation}[{_defaultColor}]{line}[/]");
            currentLine++;
        }
    }

    private void WriteEmphasisInline(EmphasisInline emphasis, string? markupTag = null)
    {
        switch (emphasis.DelimiterChar)
        {
            case '*':
                markupTag += "bold ";
                WriteInlines(emphasis, markupTag);
                break;

            case '_':
                markupTag += "italic ";
                WriteInlines(emphasis, markupTag);
                break;

            case '~':
                markupTag += "strikethrough ";
                WriteInlines(emphasis, markupTag);
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

        sb.Append("[{_highlighted}]");
        sb.Append(_characterSet.InlineCodeOpening);
        sb.Append("[invert]");
        sb.Append(code.Content.EscapeMarkup());
        sb.Append("[/]");
        sb.Append(_characterSet.InlineCodeClosing);
        sb.Append("[/]");

        _console.Markup(sb.ToString());
    }
}