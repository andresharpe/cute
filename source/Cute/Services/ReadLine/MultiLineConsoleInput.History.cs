namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static void NextHistoryEntry(InputState state, ConsoleKeyInfo input)
    {
        if (_historyEntry < _history.Count)
        {
            _historyEntry++;
            if (_historyEntry < _history.Count)
            {
                state.BufferLines = new(_history[_historyEntry].BufferLines);
                state.BufferPos.Row = Math.Max(0, _history[_historyEntry].BufferLines.Count - 1);
                state.BufferPos.Column = _history[_historyEntry].BufferLines.Last().Length;
            }
            else
            {
                state.BufferLines = new();
                state.BufferPos.Row = 0;
                state.BufferPos.Column = 0;
            }
            state.IsDisplayValid = false;
        }
    }

    private static void PreviousHistoryEntry(InputState state, ConsoleKeyInfo input)
    {
        if (_historyEntry > 0)
        {
            _historyEntry--;
            state.BufferLines = new(_history[_historyEntry].BufferLines);
            state.BufferPos.Row = Math.Max(0, _history[_historyEntry].BufferLines.Count - 1);
            state.BufferPos.Column = _history[_historyEntry].BufferLines.Last().Length;
            state.IsDisplayValid = false;
        }
    }
}