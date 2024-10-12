namespace Cute.Services.ReadLine.Commands;

internal class InsertCharacterCommand(MultiLineConsoleInput.InputState state, char character) : IUndoableCommand
{
    private readonly char _char = character;
    private readonly int _row = state.BufferPos.Row;
    private readonly int _column = state.BufferPos.Column;
    private readonly MultiLineConsoleInput.InputState _state = state;

    public void Execute()
    {
        _state.BufferLines.Insert(_row, _column, _char);
        _state.BufferPos.Column++;
    }

    public void Undo()
    {
        _state.BufferLines.Remove(_row, _column, 1);
        _state.BufferPos.Row = _row;
        _state.BufferPos.Column = _column;
    }
}