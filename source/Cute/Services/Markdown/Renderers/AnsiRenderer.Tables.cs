using Cute.Constants;
using Markdig.Syntax;
using Spectre.Console;
using System.Text.RegularExpressions;
using MarkdownTable = Markdig.Extensions.Tables;

namespace Cute.Services.Markdown.Console.Renderers;

public partial class AnsiRenderer
{
    private void WriteTableBlock(MarkdownTable.Table block)
    {
        var table = new Table().Border(TableBorder.Rounded);

        using var buffer = new StringWriter();

        var subRenderer = new AnsiRendererBuilder()
            .RedirectOutput(buffer)
            .Build();

        foreach (var row in block.OfType<MarkdownTable.TableRow>())
        {
            if (row.IsHeader)
            {
                foreach (var cell in row.OfType<MarkdownTable.TableCell>())
                {
                    foreach (var paragraph in cell.OfType<ParagraphBlock>())
                    {
                        buffer.GetStringBuilder().Clear();

                        subRenderer.WriteParagraphBlock(paragraph, suppressNewLine: true);

                        var escapedBuffer = AnsiConsoleToTextOnly(buffer.ToString()).EscapeMarkup();

                        table.AddColumn(new TableColumn(new Text(escapedBuffer, Globals.StyleAlertAccent)));

                        break;
                    }
                }
                continue;
            }

            var rowValues = new List<Markup>();

            foreach (var cell in row.OfType<MarkdownTable.TableCell>())
            {
                foreach (var paragraph in cell.OfType<ParagraphBlock>())
                {
                    buffer.GetStringBuilder().Clear();

                    subRenderer.WriteParagraphBlock(paragraph, suppressNewLine: true);

                    var escapedBuffer = AnsiConsoleToTextOnly(buffer.ToString()).EscapeMarkup();

                    rowValues.Add(new Markup(escapedBuffer, Globals.StyleSubHeading));
                }
            }

            if (rowValues.Count > 0)
            {
                table.AddRow(rowValues);
            }
        }

        _console.Write(table);
    }
}

// end of class