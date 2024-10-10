using System.Text;
using TextCopy;

namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private class InputState
    {
#pragma warning disable IDE1006 // Naming Styles
        internal bool IsDone = false;
        internal int DisplayWidth = 0;
        internal int RenderStartRow = Console.CursorTop;
        internal int RenderStartColumn = Console.CursorLeft;
        internal int RenderEndRow = Console.CursorTop;
        internal bool IsDisplayValid = false;
        internal List<StringBuilder> BufferLines = [new()];
        internal List<(int Row, int Column, string Line)> DisplayLines = [];
        internal ConsoleCursor BufferPos = new();
        internal ConsoleCursor BufferPreviousPos = new();
        internal ConsoleCursor BufferSelectStartPos = new();
        internal ConsoleCursor BufferSelectEndPos = new();
        internal ConsoleCursor DisplayPos = new();
        internal ConsoleCursor DisplaySelectStartPos = new();
        internal ConsoleCursor DisplaySelectEndPos = new();
        internal Clipboard Clipboard = new();
        internal Stack<(List<StringBuilder> Lines, int CursorLine, int CursorColumn)> UndoStack = new();
        internal Stack<(List<StringBuilder> Lines, int CursorLine, int CursorColumn)> RedoStack = new();
        internal ConsoleKeyInfo InputKeyInfo = new();
        internal bool IsSelecting = false;
        internal int VerticalDisplayColumn = 0;

#pragma warning restore IDE1006 // Naming Styles
    }
}