using Markdig.Syntax;
using Spectre.Console;

namespace Cute.Services.Markdown.Console.Renderers;

public partial class AnsiRenderer
{
    private void WriteQuoteBlock(QuoteBlock block)
    {
        foreach (var subBlock in block)
        {
            if (subBlock is ParagraphBlock paragraph)
            {
                _isQuote = true;

                _console.Markup($"[{_highlighted}] {_characterSet.QuotePrefix} [/]");
                WriteParagraphBlock(paragraph);

                _isQuote = false;
                return;
            }

            // We shouldn't be able to get here.
            ThrowOrFallbackToPlainText(subBlock);
        }
    }
}
