namespace Cute.Services.ReadLine.Commands;

internal class DeleteLinesCommand(MultiLineConsoleInput.InputState state, int? startLine = null, int? endLine = null) : IUndoableCommand
{
    private readonly MultiLineConsoleInput.InputState _state = state;
    private readonly int _row = state.BufferPos.Row;
    private readonly int _col = state.BufferPos.Column;
    private readonly int _startLine = startLine ?? 0;
    private readonly int _endLine = endLine ?? state.BufferLines.Count - 1;

    private readonly string[] _lines = state.BufferLines
        .Skip(startLine ?? 0)
        .Take((endLine ?? state.BufferLines.Count - 1) - (startLine ?? 0) + 1)
        .ToArray();

    public void Execute()
    {
        _state.BufferLines.RemoveLines(_startLine, _endLine);
        _state.BufferPos.Column = 0;
        _state.BufferPos.Row = _startLine;
    }

    public void Undo()
    {
        _state.BufferLines.InsertLines(_startLine, _lines);
        _state.BufferPos.Row = _row;
        _state.BufferPos.Column = _col;
    }
}