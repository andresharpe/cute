namespace Cute.Services.ReadLine.Commands;

internal class DeleteCharacterCommand(MultiLineConsoleInput.InputState state) : IUndoableCommand
{
    private readonly char _char = state.BufferLines.GetLineSpan(state.BufferPos.Row, true).Span[state.BufferPos.Column];
    private readonly int _row = state.BufferPos.Row;
    private readonly int _column = state.BufferPos.Column;
    private readonly MultiLineConsoleInput.InputState _state = state;

    public void Execute()
    {
        _state.BufferLines.Remove(_row, _column, 1);
    }

    public void Undo()
    {
        _state.BufferLines.Insert(_row, _column, _char);
        _state.BufferPos.Row = _row;
        _state.BufferPos.Column = _column;
    }
}