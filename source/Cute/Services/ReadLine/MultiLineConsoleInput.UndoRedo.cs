using System.Text;

namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static void SaveUndoState(InputState state, bool clearRedoStack = true)
    {
        state.UndoStack.Push((state.BufferLines.Select(sb => new StringBuilder(sb.ToString())).ToList(), state.BufferPos.Row, state.BufferPos.Column));
        if (clearRedoStack) state.RedoStack.Clear();
    }

    private static void SaveRedoState(InputState state)
    {
        state.RedoStack.Push((state.BufferLines.Select(sb => new StringBuilder(sb.ToString())).ToList(), state.BufferPos.Row, state.BufferPos.Column));
    }

    private static void RedoChanges(InputState state)
    {
        if (state.RedoStack.Count > 0)
        {
            SaveUndoState(state, false);
            var (Lines, Row, Column) = state.RedoStack.Pop();
            state.BufferLines = Lines;
            state.BufferPos.Row = Row;
            state.BufferPos.Column = Column;
            state.IsSelecting = false;
            state.IsDisplayValid = false;
        }
    }

    private static void UndoChanges(InputState state)
    {
        if (state.UndoStack.Count > 0)
        {
            SaveRedoState(state);
            var (Lines, Row, Column) = state.UndoStack.Pop();
            state.BufferLines = Lines;
            state.BufferPos.Row = Row;
            state.BufferPos.Column = Column;
            state.IsSelecting = false;
            state.IsDisplayValid = false;
        }
    }
}