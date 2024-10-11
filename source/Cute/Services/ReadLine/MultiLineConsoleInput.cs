using System.Drawing;

namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static readonly List<InputState> _history = [];

    private static int _historyEntry = 0;

    public static string? ReadLine(string prompt = "> ",
        Color? promptForeground = null,
        Color? textForeground = null,
        Color? textBackground = null
    )
    {
        var options = new InputOptions()
        {
            Prompt = prompt,
            PromptForeground = promptForeground ?? Color.DarkOrange,
            TextForeground = textForeground ?? Color.FromArgb(192, 192, 192),
            TextBackground = textBackground ?? Color.FromArgb(40, 40, 40)
        };
        return ReadLine(options);
    }

    public static string? ReadLine(InputOptions options)
    {
        var state = new InputState();

        _historyEntry = _history.Count;

        Console.TreatControlCAsInput = true;

        while (!state.IsDone)
        {
            Render(state, options);

            state.InputKeyInfo = Console.ReadKey(intercept: true);

            ProcessInput(state, options);
        }

        Console.TreatControlCAsInput = false;

        if (state.RenderStartRow + state.DisplayLines.Count >= Console.BufferHeight)
        {
            Console.SetCursorPosition(Console.BufferWidth - 1, Console.BufferHeight - 1);
            Console.WriteLine();
            state.RenderStartRow--;
            state.RenderEndRow--;
        }

        Console.SetCursorPosition(state.RenderStartColumn,
            state.RenderStartRow + state.DisplayLines.Count);

        if (state.InputKeyInfo.Key == ConsoleKey.Escape)
        {
            return null;
        }

        state.UndoStack.Clear();
        state.RedoStack.Clear();
        state.DisplayLines.Clear();
        state.Clipboard = null!;
        _history.Add(state);

        return string.Join("\n", state.BufferLines);
    }

    private static void ProcessInput(InputState state, InputOptions options)
    {
        var input = state.InputKeyInfo;

        // Handle keyboard inputs
        if (input.Key == ConsoleKey.F1)
        {
            DisplayHelp(state, options);
        }
        else if (input.Key == ConsoleKey.C && input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + C: Copy
            CopyToClipboard(state);
        }
        else if (input.Key == ConsoleKey.X && input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + X: Cut
            CutToClipboard(state);
        }
        else if ((input.Key == ConsoleKey.V || input.Key == ConsoleKey.B) && input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + V: Paste
            PasteFromClipboard(state);
        }
        else if (input.Key == ConsoleKey.Z && input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + Z: Undo
            UndoChanges(state);
        }
        else if (input.Key == ConsoleKey.Y && input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + Y: Redo
            RedoChanges(state);
        }
        else if (input.Key == ConsoleKey.A && input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + A: Select All
            SelectAll(state);
        }
        else if (input.Key == ConsoleKey.LeftArrow)
        {
            // Left Arrow
            MoveCursorLeft(state, input);
        }
        else if (input.Key == ConsoleKey.RightArrow)
        {
            // Right Arrow
            MoveCursorRight(state, input);
        }
        else if (input.Key == ConsoleKey.PageUp || (input.Key == ConsoleKey.UpArrow && input.Modifiers.HasFlag(ConsoleModifiers.Control)))
        {
            // PgUp - previous history entry
            PreviousHistoryEntry(state, input);
        }
        else if (input.Key == ConsoleKey.PageDown || (input.Key == ConsoleKey.DownArrow && input.Modifiers.HasFlag(ConsoleModifiers.Control)))
        {
            // PgDn - next history entry
            NextHistoryEntry(state, input);
        }
        else if (input.Key == ConsoleKey.UpArrow)
        {
            // Up Arrow
            MoveCursorUp(state, input);
        }
        else if (input.Key == ConsoleKey.DownArrow)
        {
            // Down Arrow
            MoveCursorDown(state, input);
        }
        else if (input.Key == ConsoleKey.Home)
        {
            // Home
            MoveCursorHome(state, input);
        }
        else if (input.Key == ConsoleKey.End)
        {
            // End
            MoveCursorEnd(state, input);
        }
        else if (input.Key == ConsoleKey.Backspace)
        {
            // Backspace
            DeleteTextBackwards(state, input);
        }
        else if (input.Key == ConsoleKey.Delete)
        {
            // Delete
            DeleteTextForwards(state, input);
        }
        else if (input.Key == ConsoleKey.Tab
            || (input.Key == ConsoleKey.Enter && input.Modifiers.HasFlag(ConsoleModifiers.Control)))
        {
            // Ctrl + Enter: Finish input
            state.IsDone = true;
        }
        else if (input.Key == ConsoleKey.Enter)
        {
            // Enter: New line
            InsertNewline(state);
        }
        else if (input.Key == ConsoleKey.Escape)
        {
            // Escape: Cancel input if empty
            ClearInputOrExit(state, options);
        }
        else if (input.KeyChar != '\0' && !char.IsControl(input.KeyChar))
        {
            // Character input
            InsertCharacter(state, input);
        }
    }
}