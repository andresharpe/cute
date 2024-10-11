namespace Cute.Services.ReadLine.Commands;

internal class InsertLinesCommand(MultiLineConsoleInput.InputState state, string lines) : IUndoableCommand
{
    private readonly string _lines = lines;
    private readonly int _row = state.BufferPos.Row;
    private readonly int _column = state.BufferPos.Column;
    private readonly MultiLineConsoleInput.InputState _state = state;
    private int _inserted = 0;

    public void Execute()
    {
        _inserted = _state.BufferLines.Insert(_row, _column, _lines);
        (_state.BufferPos.Row, _state.BufferPos.Column) =
            _state.BufferLines.GetLastPositionAfterInsert(_row, _column, _lines);
    }

    public void Undo()
    {
        _state.BufferLines.Remove(_row, _column, _inserted);
        _state.BufferPos.Column = _column;
        _state.BufferPos.Row = _row;
    }
}