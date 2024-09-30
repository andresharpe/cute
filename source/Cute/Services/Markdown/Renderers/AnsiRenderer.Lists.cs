using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Spectre.Console;

namespace Cute.Services.Markdown.Console.Renderers;

public partial class AnsiRenderer
{
    private void WriteListBlock(ListBlock block, int indent = 0)
    {
        var numberedListCounter = 1;

        foreach (var item in block)
        {
            var indentation = new string(' ', indent * 3);
            var listBullet = $"{indentation} [{_highlighted}]{_characterSet.ListBullet}[/]  ";
            var bullet = (block.BulletType) switch
            {
                '-' => IsTaskList(item) ? "{indentation}  " : listBullet,
                '1' => $"{indentation} [{_highlighted}]{_numberFormatter.Format(numberedListCounter++, _characterSet)}. [/]",
                _ => listBullet
            };

            _console.Markup(bullet);

            foreach (var subItem in (ListItemBlock)item)
            {
                if (subItem is ParagraphBlock paragraphBlock)
                {
                    WriteParagraphBlock(paragraphBlock, indent: indent);
                    continue;
                }
                else if (subItem is ListBlock subListBlock)
                {
                    WriteListBlock(subListBlock, indent + 1);
                    continue;
                }

                ThrowOrFallbackToPlainText(subItem);
            }
        }
        _console.WriteLine();

        static bool IsTaskList(Block itemToCheck)
        {
            return itemToCheck.Descendants().OfType<TaskList>().Any();
        }
    }
}