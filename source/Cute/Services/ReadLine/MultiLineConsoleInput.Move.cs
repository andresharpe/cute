namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static void MoveCursorEnd(InputState state, ConsoleKeyInfo input)
    {
        state.BufferPreviousPos.Column = state.BufferPos.Column;
        state.BufferPreviousPos.Row = state.BufferPos.Row;

        if (input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            state.BufferPos.Row = state.BufferLines.Count - 1;
            state.BufferPos.Column = state.BufferLines[state.BufferPos.Row].Length;
        }
        else
        {
            state.BufferPos.Column = state.BufferLines[state.BufferPos.Row].Length;
        }
        UpdateSelection(state, input.Modifiers.HasFlag(ConsoleModifiers.Shift));
    }

    private static void MoveCursorHome(InputState state, ConsoleKeyInfo input)
    {
        state.BufferPreviousPos.Column = state.BufferPos.Column;
        state.BufferPreviousPos.Row = state.BufferPos.Row;

        if (input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            state.BufferPos.Row = 0;
            state.BufferPos.Column = 0;
        }
        else
        {
            state.BufferPos.Column = 0;
        }
        UpdateSelection(state, input.Modifiers.HasFlag(ConsoleModifiers.Shift));
    }

    private static void MoveCursorDown(InputState state, ConsoleKeyInfo input)
    {
        state.BufferPreviousPos.Column = state.BufferPos.Column;
        state.BufferPreviousPos.Row = state.BufferPos.Row;

        if (state.DisplayPos.Row < state.DisplayLines.Count - 1)
        {
            state.DisplayPos.Row++;
            UpdateDisplayColumn(state);
        }
        UpdateSelection(state, input.Modifiers.HasFlag(ConsoleModifiers.Shift));
    }

    private static void MoveCursorUp(InputState state, ConsoleKeyInfo input)
    {
        state.BufferPreviousPos.Column = state.BufferPos.Column;
        state.BufferPreviousPos.Row = state.BufferPos.Row;

        if (state.DisplayPos.Row > 0)
        {
            state.DisplayPos.Row--;
            UpdateDisplayColumn(state);
        }
        UpdateSelection(state, input.Modifiers.HasFlag(ConsoleModifiers.Shift));
    }

    private static void MoveCursorRight(InputState state, ConsoleKeyInfo input)
    {
        state.BufferPreviousPos.Column = state.BufferPos.Column;
        state.BufferPreviousPos.Row = state.BufferPos.Row;

        if (input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + Right Arrow: Move to next word
            MoveCursorToNextWord(state);
        }
        else
        {
            if (state.BufferPos.Column < state.BufferLines[state.BufferPos.Row].Length)
                state.BufferPos.Column++;
            else if (state.BufferPos.Row < state.BufferLines.Count - 1)
            {
                state.BufferPos.Row++;
                state.BufferPos.Column = 0;
            }
        }
        UpdateSelection(state, input.Modifiers.HasFlag(ConsoleModifiers.Shift));
    }

    private static void MoveCursorLeft(InputState state, ConsoleKeyInfo input)
    {
        state.BufferPreviousPos.Column = state.BufferPos.Column;
        state.BufferPreviousPos.Row = state.BufferPos.Row;

        if (input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + Left Arrow: Move to previous word
            MoveCursorToPreviousWord(state);
        }
        else
        {
            if (state.BufferPos.Column > 0)
                state.BufferPos.Column--;
            else if (state.BufferPos.Row > 0)
            {
                state.BufferPos.Row--;
                state.BufferPos.Column = state.BufferLines[state.BufferPos.Row].Length;
            }
        }

        UpdateSelection(state, input.Modifiers.HasFlag(ConsoleModifiers.Shift));
    }

    private static void MoveCursorToPreviousWord(InputState state)
    {
        if (state.BufferPos.Column == 0 && state.BufferPos.Row > 0)
        {
            state.BufferPos.Row--;
            state.BufferPos.Column = state.BufferLines[state.BufferPos.Row].Length;
        }

        var row = state.BufferLines[state.BufferPos.Row].Span;

        while (state.BufferPos.Column > 0 && char.IsWhiteSpace(row[state.BufferPos.Column - 1]))
            state.BufferPos.Column--;

        while (state.BufferPos.Column > 0 && !char.IsWhiteSpace(row[state.BufferPos.Column - 1]))
            state.BufferPos.Column--;
    }

    private static void MoveCursorToNextWord(InputState state)
    {
        var row = state.BufferLines[state.BufferPos.Row].Span;

        int lineLength = row.Length;

        while (state.BufferPos.Column < lineLength && !char.IsWhiteSpace(row[state.BufferPos.Column]))
            state.BufferPos.Column++;

        while (state.BufferPos.Column < lineLength && char.IsWhiteSpace(row[state.BufferPos.Column]))
            state.BufferPos.Column++;

        if (state.BufferPos.Column == lineLength && state.BufferPos.Row < state.BufferLines.Count - 1)
        {
            state.BufferPos.Row++;
            state.BufferPos.Column = 0;
        }
    }
}