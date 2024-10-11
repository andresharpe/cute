namespace Cute.Services.ReadLine.Commands;

internal class DeleteSelectedTextCommand : IUndoableCommand
{
    private readonly int _selectStartPosRow;
    private readonly int _selectStartPosColumn;
    private readonly int _selectEndPosRow;
    private readonly int _selectEndPosColumn;
    private readonly int _row;
    private readonly int _column;

    private readonly string _lines;

    private readonly MultiLineConsoleInput.InputState _state;

    public DeleteSelectedTextCommand(MultiLineConsoleInput.InputState state, int? currentRow = null, int? currentColumn = null)
    {
        _selectStartPosRow = state.BufferSelectStartPos.Row;
        _selectStartPosColumn = state.BufferSelectStartPos.Column;
        _selectEndPosRow = state.BufferSelectEndPos.Row;
        _selectEndPosColumn = state.BufferSelectEndPos.Column;
        _state = state;
        _row = currentRow ?? _state.BufferPos.Row;
        _column = currentColumn ?? _state.BufferPos.Column;
        _lines = _state.BufferLines.GetRange(_selectStartPosRow, _selectStartPosColumn,
            _selectEndPosRow, _selectEndPosColumn);
    }

    public void Execute()
    {
        _state.BufferLines.Remove(_selectStartPosRow, _selectStartPosColumn,
            _selectEndPosRow, _selectEndPosColumn);
        _state.BufferPos.Row = _selectStartPosRow;
        _state.BufferPos.Column = Math.Max(0, _selectStartPosColumn - 1);
    }

    public void Undo()
    {
        _state.BufferLines.Insert(_selectStartPosRow, _selectStartPosColumn, _lines);
        _state.BufferPos.Row = _row;
        _state.BufferPos.Column = _column;
    }
}