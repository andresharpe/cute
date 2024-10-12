namespace Cute.Services.ReadLine.Commands;

public interface IUndoableCommand
{
    void Execute();

    void Undo();
}