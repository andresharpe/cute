using Cute.Constants;
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
                _console
                    .Write(new Text(escapedHeader, Globals.StyleHeading));
                return;
            }

            // Levels 2 through 6 are rendered with a common format.
            // We could differentiate by colour and font weight.
            _console.MarkupLine($"[bold {_highlighted}]{escapedHeader}[/]");
        }
    }
}