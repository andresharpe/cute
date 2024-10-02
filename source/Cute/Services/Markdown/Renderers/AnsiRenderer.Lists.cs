using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Spectre.Console;

namespace Cute.Services.Markdown.Console.Renderers;

public partial class AnsiRenderer
{
    private void WriteListBlock(ListBlock block)
    {
        var numberedListCounter = 1;

        var lastIndentation = GetIndentation();

        Indent();

        var indentation = GetIndentation();

        foreach (var item in block)
        {
            var listBullet = $"{lastIndentation}  [{_accentColor}]{_characterSet.ListBullet}[/] ";
            var numberPadding = (numberedListCounter < 10 ? " " : string.Empty);
            var numberDigits = _numberFormatter.Format(numberedListCounter, _characterSet);
            var bullet = (block.BulletType) switch
            {
                '-' => IsTaskList(item) ? indentation : listBullet,
                '1' => $"{lastIndentation}[{_accentColor}]{numberPadding}{numberDigits}. [/]",
                _ => listBullet
            };

            if (numberedListCounter > 1)
            {
                _console.WriteLine();
            }

            _console.Markup(bullet);

            foreach (var subItem in (ListItemBlock)item)
            {
                WriteBlock(subItem, indentFirstLine: false);
            }

            numberedListCounter++;
        }

        _console.WriteLine();

        UnIndent();

        static bool IsTaskList(Block itemToCheck)
        {
            return itemToCheck.Descendants().OfType<TaskList>().Any();
        }
    }
}