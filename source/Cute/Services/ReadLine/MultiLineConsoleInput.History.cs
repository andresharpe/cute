using System.Text;

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
                state.BufferLines = _history[_historyEntry].BufferLines.Select(sb => new StringBuilder(sb.ToString())).ToList();
                state.BufferPos.Row = Math.Max(0, _history[_historyEntry].BufferLines.Count - 1);
                state.BufferPos.Column = _history[_historyEntry].BufferLines.Last().Length;
            }
            else
            {
                state.BufferLines = [new()];
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
            state.BufferLines = _history[_historyEntry].BufferLines.Select(sb => new StringBuilder(sb.ToString())).ToList();
            state.BufferPos.Row = Math.Max(0, _history[_historyEntry].BufferLines.Count - 1);
            state.BufferPos.Column = _history[_historyEntry].BufferLines.Last().Length;
            state.IsDisplayValid = false;
        }
    }
}