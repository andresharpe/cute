using Cute.Services.ReadLine.Commands;

namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static void InsertCharacter(InputState state, char inputChar)
    {
        if (state.IsSelecting)
        {
            DeleteSelectedText(state);
            state.IsSelecting = false;
        }

        state.ExecuteCommand(new InsertCharacterCommand(state, inputChar));

        state.IsDisplayValid = false;
    }

    private static void InsertString(InputState state, string inputString)
    {
        if (state.IsSelecting)
        {
            DeleteSelectedText(state);
            state.IsSelecting = false;
        }

        InsertLines(state, inputString);

        state.IsDisplayValid = false;
    }

    private static void ClearInputOrExit(InputState state, InputOptions options)
    {
        if (state.BufferLines.Count == 1 && state.BufferLines[0].Length == 0)
        {
            Render(state, options);
            state.IsDone = true;
        }
        else
        {
            state.ExecuteCommand(new DeleteLinesCommand(state));
            state.IsSelecting = false;
            state.IsDisplayValid = false;
        }
    }

    private static void InsertNewline(InputState state)
    {
        if (state.IsSelecting)
        {
            DeleteSelectedText(state);
            state.IsSelecting = false;
        }
        state.ExecuteCommand(new InsertNewlineCommand(state));
        state.IsDisplayValid = false;
    }

    private static void DeleteTextForwards(InputState state, ConsoleKeyInfo input)
    {
        state.BufferPreviousPos.Column = state.BufferPos.Column;
        state.BufferPreviousPos.Row = state.BufferPos.Row;

        if (input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + Delete: Delete next word
            DeleteNextWord(state);
        }
        else
        {
            if (state.IsSelecting)
            {
                DeleteSelectedText(state);
                state.IsSelecting = false;
            }
            else
            {
                state.ExecuteCommand(new DeleteCharacterCommand(state));
            }
        }
        state.IsDisplayValid = false;
    }

    private static void DeleteTextBackwards(InputState state, ConsoleKeyInfo input)
    {
        state.BufferPreviousPos.Column = state.BufferPos.Column;
        state.BufferPreviousPos.Row = state.BufferPos.Row;

        if (input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + Backspace: Delete previous word
            DeletePreviousWord(state);
        }
        else
        {
            if (state.IsSelecting)
            {
                DeleteSelectedText(state);
                state.IsSelecting = false;
            }
            else
            {
                if (state.BufferPos.Row > 0 || state.BufferPos.Column > 0)
                {
                    MoveCursorLeft(state, new ConsoleKeyInfo());
                    state.ExecuteCommand(new DeleteCharacterCommand(state));
                }
            }
        }
        state.IsDisplayValid = false;
    }

    private static void InsertLines(InputState state, string lines)
    {
        if (lines.Length == 0) return;

        state.ExecuteCommand(new InsertLinesCommand(state, lines));
    }

    private static void DeletePreviousWord(InputState state)
    {
        state.BufferPreviousPos.Column = state.BufferPos.Column;
        state.BufferPreviousPos.Row = state.BufferPos.Row;

        state.BufferSelectEndPos.Column = Math.Max(0, state.BufferPos.Column - 1);
        state.BufferSelectEndPos.Row = state.BufferPos.Row;

        MoveCursorToPreviousWord(state);

        state.BufferSelectStartPos.Column = state.BufferPos.Column;
        state.BufferSelectStartPos.Row = state.BufferPos.Row;

        DeleteTextRange(state);
    }

    private static void DeleteNextWord(InputState state)
    {
        var cursorColumn = state.BufferPos.Column;

        state.BufferPreviousPos.Column = state.BufferPos.Column;
        state.BufferPreviousPos.Row = state.BufferPos.Row;

        state.BufferSelectStartPos.Column = state.BufferPos.Column;
        state.BufferSelectStartPos.Row = state.BufferPos.Row;

        MoveCursorToNextWord(state);

        state.BufferSelectEndPos.Column = Math.Max(0, state.BufferPos.Column - 1);
        state.BufferSelectEndPos.Row = state.BufferPos.Row;

        DeleteTextRange(state);

        state.BufferPos.Column = cursorColumn;
    }

    private static void DeleteSelectedText(InputState state)
    {
        DeleteTextRange(state);

        state.BufferPos.Row = state.BufferSelectStartPos.Row;
        state.BufferPos.Column = state.BufferSelectStartPos.Column;
    }

    private static void DeleteTextRange(InputState state)
    {
        if (state.BufferSelectStartPos.Row == state.BufferSelectEndPos.Row
            && state.BufferSelectStartPos.Column == state.BufferSelectEndPos.Column
            && state.BufferSelectStartPos.Row == 0
            && state.BufferSelectStartPos.Column == 0
            && state.BufferLines.GetLineSpan(0).Length == 0)
        {
            return;
        }

        state.ExecuteCommand(
            new DeleteSelectedTextCommand(state,
                state.BufferPreviousPos.Row,
                state.BufferPreviousPos.Column)
        );
    }
}