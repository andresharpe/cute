using Markdig.Syntax;
using Spectre.Console;

namespace Cute.Services.Markdown.Console.Renderers;

public partial class AnsiRenderer
{
    internal void WriteParagraphBlock(ParagraphBlock block,
        bool suppressNewLine = false, string? markupTag = null, bool indentFirstLine = true)
    {
        if (block.Inline is not null)
        {
            WriteInlines(block.Inline, markupTag, indentFirstLine);

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