using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Cute.Services.Markdown.Console.Extensions;
using Spectre.Console;

namespace Cute.Services.Markdown.Console.Renderers;

public partial class AnsiRenderer
{
    internal void WriteParagraphBlock(ParagraphBlock block,
        bool suppressNewLine = false, string? markupTag = null, int indent = 0)
    {
        if (block.Inline is not null)
        {
            WriteInlines(block.Inline, markupTag, indent: indent);

            if (!suppressNewLine)
            {
                _console.Write("\n");
            }

            return;
        }

        // We shouldn't be able to get here.
        ThrowOrFallbackToPlainText(block);
    }
}