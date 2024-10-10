using System.Text;

namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static void HandleCharacterInput(InputState state, ConsoleKeyInfo input)
    {
        if (state.IsSelecting)
        {
            SaveUndoState(state);
            DeleteSelectedText(state);
            state.IsSelecting = false;
        }
        SaveUndoState(state);
        state.BufferLines[state.BufferPos.Row] = state.BufferLines[state.BufferPos.Row].Insert(state.BufferPos.Column++, input.KeyChar);
        state.IsDisplayValid = false;
    }

    private static void HandleEscape(InputState state, InputOptions options)
    {
        if (state.BufferLines.Count == 1 && state.BufferLines[0].Length == 0)
        {
            Render(state, options);
            state.IsDone = true;
        }
        else
        {
            SaveUndoState(state);
            state.BufferLines.Clear();
            state.BufferLines.Add(new());
            state.BufferPos.Column = 0;
            state.BufferPos.Row = 0;
            state.IsSelecting = false;
            state.IsDisplayValid = false;
        }
    }

    private static void HandleEnter(InputState state)
    {
        if (state.IsSelecting)
        {
            SaveUndoState(state);
            DeleteSelectedText(state);
            state.IsSelecting = false;
        }
        // Push the current state onto the undo stack
        SaveUndoState(state);
        var remaining = new StringBuilder(state.BufferLines[state.BufferPos.Row].ToString(state.BufferPos.Column, state.BufferLines[state.BufferPos.Row].Length - state.BufferPos.Column));
        state.BufferLines[state.BufferPos.Row].Length = state.BufferPos.Column;
        state.BufferLines.Insert(++state.BufferPos.Row, remaining);
        state.BufferPos.Column = 0;
        state.IsDisplayValid = false;
    }

    private static void HandleDelete(InputState state, ConsoleKeyInfo input)
    {
        if (input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + Delete: Delete next word
            SaveUndoState(state);
            DeleteNextWord(state);
        }
        else
        {
            if (state.IsSelecting)
            {
                SaveUndoState(state);
                DeleteSelectedText(state);
                state.IsSelecting = false;
            }
            else
            {
                if (state.BufferPos.Column < state.BufferLines[state.BufferPos.Row].Length)
                {
                    SaveUndoState(state);
                    state.BufferLines[state.BufferPos.Row] = state.BufferLines[state.BufferPos.Row].Remove(state.BufferPos.Column, 1);
                }
                else if (state.BufferPos.Row < state.BufferLines.Count - 1)
                {
                    SaveUndoState(state);
                    state.BufferLines[state.BufferPos.Row].Append(state.BufferLines[state.BufferPos.Row + 1]);
                    state.BufferLines.RemoveAt(state.BufferPos.Row + 1);
                }
            }
        }
        state.IsDisplayValid = false;
    }

    private static void HandleBackspace(InputState state, ConsoleKeyInfo input)
    {
        if (input.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            // Ctrl + Backspace: Delete previous word
            SaveUndoState(state);
            DeletePreviousWord(state);
        }
        else
        {
            if (state.IsSelecting)
            {
                SaveUndoState(state);
                DeleteSelectedText(state);
                state.IsSelecting = false;
            }
            else
            {
                if (state.BufferPos.Column > 0)
                {
                    SaveUndoState(state);
                    state.BufferLines[state.BufferPos.Row] = state.BufferLines[state.BufferPos.Row].Remove(--state.BufferPos.Column, 1);
                }
                else if (state.BufferPos.Row > 0)
                {
                    SaveUndoState(state);
                    state.BufferPos.Column = state.BufferLines[--state.BufferPos.Row].Length;
                    state.BufferLines[state.BufferPos.Row].Append(state.BufferLines[state.BufferPos.Row + 1]);
                    state.BufferLines.RemoveAt(state.BufferPos.Row + 1);
                }
            }
        }
        state.IsDisplayValid = false;
    }

    private static void DeletePreviousWord(InputState state)
    {
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

        state.BufferSelectStartPos.Column = state.BufferPos.Column;
        state.BufferSelectStartPos.Row = state.BufferPos.Row;

        MoveCursorToNextWord(state);

        state.BufferSelectEndPos.Column = state.BufferPos.Column - 1;
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
        if (state.BufferSelectStartPos.Row == state.BufferSelectEndPos.Row)
        {
            if (state.BufferLines[state.BufferSelectStartPos.Row].Length > 0)
            {
                state.BufferLines[state.BufferSelectStartPos.Row]
                    .Remove(state.BufferSelectStartPos.Column, state.BufferSelectEndPos.Column - state.BufferSelectStartPos.Column + 1);
            }
        }
        else
        {
            state.BufferLines[state.BufferSelectStartPos.Row].Length = state.BufferSelectStartPos.Column;
            state.BufferLines[state.BufferSelectStartPos.Row].Append(state.BufferLines[state.BufferSelectEndPos.Row].ToString(state.BufferSelectEndPos.Column, state.BufferLines[state.BufferSelectEndPos.Row].Length - state.BufferSelectEndPos.Column));
            state.BufferLines.RemoveRange(state.BufferSelectStartPos.Row + 1, state.BufferSelectEndPos.Row - state.BufferSelectStartPos.Row);
        }
    }
}