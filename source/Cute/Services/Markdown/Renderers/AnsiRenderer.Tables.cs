using Markdig.Syntax;
using Spectre.Console;
using MarkdownTable = Markdig.Extensions.Tables;

namespace Cute.Services.Markdown.Console.Renderers;

public partial class AnsiRenderer
{
    private void WriteTableBlock(MarkdownTable.Table block)
    {
        var table = new Table().Border(TableBorder.Rounded);

        foreach (var item in block)
        {
            if (item is MarkdownTable.TableRow row)
            {
                var rows = new List<Markup>();

                foreach (var cellItem in row)
                {
                    if (cellItem is MarkdownTable.TableCell cell)
                    {
                        foreach (var paragraphItem in cell)
                        {
                            if (paragraphItem is ParagraphBlock paragraph)
                            {
                                // We use a new instace of this class to generate the contents of
                                // each cell.  This is because we need to avoid calling write and
                                // markup methods while building our table.
                                var buffer = new StringWriter();
                                var subRenderer = new AnsiRendererBuilder()
                                    .RedirectOutput(buffer)
                                    .Build();
                                subRenderer.WriteParagraphBlock(paragraph, suppressNewLine: true);

                                var escapedBuffer = buffer.ToString().EscapeMarkup();
                                if (row.IsHeader)
                                {
                                    table.AddColumn($"[{_highlighted}]{escapedBuffer}[/]");
                                }
                                else
                                {
                                    rows.Add(new Markup(escapedBuffer));
                                }
                            }
                        }
                    }

                }

                if (rows.Any())
                {
                    table.AddRow(rows);
                }
            }
        }

        _console.Write(table);
    }
}
