namespace Cute.Services.ReadLine.Commands;

internal class InsertNewlineCommand(MultiLineConsoleInput.InputState state) : IUndoableCommand
{
    private readonly int _row = state.BufferPos.Row;
    private readonly int _column = state.BufferPos.Column;
    private readonly MultiLineConsoleInput.InputState _state = state;

    public void Execute()
    {
        _state.BufferLines.Insert(_row, _column, '\n');
        _state.BufferPos.Row++;
        _state.BufferPos.Column = 0;
    }

    public void Undo()
    {
        _state.BufferLines.Remove(_row, _column, 1);
        _state.BufferPos.Row = _row;
        _state.BufferPos.Column = _column;
    }
}