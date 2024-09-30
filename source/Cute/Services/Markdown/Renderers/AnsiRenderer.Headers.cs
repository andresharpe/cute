using Markdig.Syntax;
using Spectre.Console;

namespace Cute.Services.Markdown.Console.Renderers;

public partial class AnsiRenderer
{
    private void WriteHeadingBlock(HeadingBlock block)
    {
        var rawContent = block.Inline?.FirstChild?.ToString();

        if (rawContent is not null)
        {
            var escapedHeader = rawContent.EscapeMarkup();

            if (block.Level == 1)
            {
                _console.MarkupLine($"[bold underline italic {_highlightedColor}]{escapedHeader}[/]");
                return;
            }

            if (block.Level == 2)
            {
                _console.MarkupLine($"[bold underline italic {_accentColor}]{escapedHeader}[/]");
                return;
            }

            if (block.Level == 3)
            {
                _console.MarkupLine($"[bold underline {_accentColor}]{escapedHeader}[/]");
                return;
            }

            _console.MarkupLine($"[bold {_accentColor}]{escapedHeader}[/]");
        }
    }
}