using Cute.Services.ReadLine.Commands;

namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static void ExecuteCommand(this InputState state, IUndoableCommand command)
    {
        command.Execute();
        state.UndoStack.Push(command);
        state.RedoStack.Clear();
    }

    private static void UndoChanges(InputState state)
    {
        if (state.UndoStack.Count > 0)
        {
            var command = state.UndoStack.Pop();
            command.Undo();
            state.RedoStack.Push(command);
            state.IsSelecting = false;
            state.IsDisplayValid = false;
        }
    }

    private static void RedoChanges(InputState state)
    {
        if (state.RedoStack.Count > 0)
        {
            var command = state.RedoStack.Pop();
            command.Execute();
            state.UndoStack.Push(command);
            state.IsSelecting = false;
            state.IsDisplayValid = false;
        }
    }
}