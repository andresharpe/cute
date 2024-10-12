using System.Runtime.InteropServices;

namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static void Render(InputState state, InputOptions options)
    {
        if (Console.KeyAvailable) return;

        var prompt = options.Prompt;

        int promptLength = prompt.Length;

        var oldDisplayWidth = state.DisplayWidth;

        state.DisplayWidth = Console.WindowWidth - prompt.Length - 1;

        state.IsDisplayValid = state.IsDisplayValid && oldDisplayWidth == state.DisplayWidth;

        if (state.IsDisplayValid && !state.IsSelecting)
        {
            UdateDisplayPositions(state);
            Console.SetCursorPosition(
                state.DisplayPos.Column + promptLength,
                state.RenderStartRow + state.DisplayPos.Row);
            return;
        }

        var ansiPromptColor = $"\x1b[38;2;{options.PromptForeground.R};{options.PromptForeground.G};{options.PromptForeground.B}m";

        var ansiTextColor = $"\x1b[38;2;{options.TextForeground.R};{options.TextForeground.G};{options.TextForeground.B};" +
            $"48;2;{options.TextBackground.R};{options.TextBackground.G};{options.TextBackground.B}m";

        var ansiResetColor = "\x1b[0m";

        var ansiInvertColor = "\x1b[7m";

        Console.CursorVisible = false;

        UpdateDisplayLines(state);

        UdateDisplayPositions(state);

        string promptPadding = new(' ', prompt.Length);

        int displayWidth = state.DisplayWidth;

        EnsureConsoleBufferHeight(state);

        for (int i = 0; i < state.DisplayLines.Count; i++)
        {
            var lineStartsWithSelect = state.IsSelecting && i > state.DisplaySelectStartPos.Row && i <= state.DisplaySelectEndPos.Row;

            var lineEndsWithSelect = state.IsSelecting && i >= state.DisplaySelectStartPos.Row && i < state.DisplaySelectEndPos.Row;

            var displayLine = state.DisplayLines[i].Line;

            var line = $"{displayLine} ";

            Console.SetCursorPosition(state.RenderStartColumn, state.RenderStartRow + i);

            Console.Write($"{ansiPromptColor}{(i == 0 ? prompt : promptPadding)}{ansiResetColor}");

            if (lineStartsWithSelect)
            {
                Console.Write(ansiInvertColor);
            }

            Console.Write(ansiTextColor);

            if (state.IsSelecting && i == state.DisplaySelectStartPos.Row && i == state.DisplaySelectEndPos.Row)
            {
                Console.Write(line[0..state.DisplaySelectStartPos.Column]);
                Console.Write(ansiInvertColor);
                Console.Write(line[state.DisplaySelectStartPos.Column..(state.DisplaySelectEndPos.Column + 1)]);
                Console.Write(ansiResetColor);
                Console.Write(ansiTextColor);
                Console.Write(line[(state.DisplaySelectEndPos.Column + 1)..]);
            }
            else if (state.IsSelecting && i == state.DisplaySelectStartPos.Row)
            {
                Console.Write(line[0..state.DisplaySelectStartPos.Column]);
                Console.Write(ansiInvertColor);
                Console.Write(line[state.DisplaySelectStartPos.Column..]);
                Console.Write(ansiResetColor);
            }
            else if (state.IsSelecting && i == state.DisplaySelectEndPos.Row)
            {
                Console.Write(line[..(state.DisplaySelectEndPos.Column + 1)]);
                Console.Write(ansiResetColor);
                Console.Write(ansiTextColor);
                Console.Write(line[(state.DisplaySelectEndPos.Column + 1)..]);
            }
            else
            {
                Console.Write(line);
            }
            if (lineEndsWithSelect)
            {
                Console.Write(ansiResetColor);
                Console.Write(ansiTextColor);
            }
            Console.Write(new string(' ', Math.Max(0, displayWidth - line.Length)));
            Console.Write(ansiResetColor);
        }

        if (state.RenderEndRow > state.RenderStartRow + state.DisplayLines.Count - 1)
        {
            var blankLine = new string(' ', displayWidth + promptLength + 1);
            for (int i = state.DisplayLines.Count; i <= state.RenderEndRow - state.RenderStartRow; i++)
            {
                Console.SetCursorPosition(state.RenderStartColumn, state.RenderStartRow + i);
                Console.Write(blankLine);
            }
            state.RenderEndRow = state.RenderStartRow + state.DisplayLines.Count - 1;
        }

        Console.SetCursorPosition(
            state.DisplayPos.Column + promptLength,
            state.RenderStartRow + state.DisplayPos.Row);

        Console.CursorVisible = true;
    }

    private static void EnsureConsoleBufferHeight(InputState state)
    {
        if (state.RenderStartRow + state.DisplayLines.Count >= Console.BufferHeight)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.BufferHeight = state.RenderStartRow + state.DisplayLines.Count;
            }
            else
            {
                for (var i = state.RenderEndRow + 1; i <= state.DisplayLines.Count; i++)
                {
                    Console.SetCursorPosition(state.RenderStartColumn, state.RenderEndRow + (i - state.RenderEndRow));
                    Console.WriteLine();
                    state.RenderStartRow--;
                    state.RenderEndRow--;
                }
            }
        }
    }

    private static void UdateDisplayPositions(InputState state)
    {
        UpdateDisplayPosition(state);
        if (state.IsSelecting)
        {
            UpdateDisplaySelectStartPosition(state);
            UpdateDisplaySelectEndPosition(state);
        }
        if (!(state.InputKeyInfo.Key == ConsoleKey.DownArrow || state.InputKeyInfo.Key == ConsoleKey.UpArrow))
        {
            state.VerticalDisplayColumn = state.DisplayPos.Column;
        }
    }

    private static void UpdateDisplayLines(InputState state)
    {
        if (state.IsDisplayValid) return;

        state.DisplayLines.Clear();
        var row = 0;
        var spanLines = state.BufferLines.GetAllSpanLines();

        foreach (var line in spanLines)
        {
            var col = 0;
            foreach (var displayLine in line.Span.GetFixedSpanLines(state.DisplayWidth))
            {
                state.DisplayLines.Add(
                    new(row, col, displayLine)
                );
                col += displayLine.Length + 1;
            }
            if (col == 0)
            {
                state.DisplayLines.Add(new(row, 0, ReadOnlyMemory<char>.Empty));
            }
            row++;
        }

        state.RenderEndRow = Math.Max(state.RenderEndRow, state.RenderStartRow + state.DisplayLines.Count - 1);

        state.IsDisplayValid = true;
    }

    private static void UpdateDisplayPosition(InputState state)
    {
        var currentDisplayLine = 0;

        foreach (var displayLine in state.DisplayLines)
        {
            var row = displayLine.Row;
            var col = displayLine.Column;
            var line = displayLine.Line;

            if (state.BufferPos.Row == row
                && state.BufferPos.Column >= col
                && state.BufferPos.Column <= col + line.Length)
            {
                state.DisplayPos.Row = currentDisplayLine;
                state.DisplayPos.Column = state.BufferPos.Column - col;
                return;
            }
            currentDisplayLine++;
        }
        state.DisplayPos.Row = -1;
        state.DisplayPos.Column = -1;
    }

    private static void UpdateDisplaySelectStartPosition(InputState state)
    {
        var currentDisplayLine = 0;

        foreach (var displayLine in state.DisplayLines)
        {
            var row = displayLine.Row;
            var col = displayLine.Column;
            var line = displayLine.Line;

            if (state.BufferSelectStartPos.Row == row
                && state.BufferSelectStartPos.Column >= col
                && state.BufferSelectStartPos.Column <= col + line.Length)
            {
                state.DisplaySelectStartPos.Row = currentDisplayLine;
                state.DisplaySelectStartPos.Column = state.BufferSelectStartPos.Column - col;
                return;
            }
            currentDisplayLine++;
        }
        state.DisplaySelectStartPos.Row = -1;
        state.DisplaySelectStartPos.Column = -1;
    }

    private static void UpdateDisplaySelectEndPosition(InputState state)
    {
        var currentDisplayLine = 0;

        foreach (var displayLine in state.DisplayLines)
        {
            var row = displayLine.Row;
            var col = displayLine.Column;
            var line = displayLine.Line;

            if (state.BufferSelectEndPos.Row == row
                && state.BufferSelectEndPos.Column >= col
                && state.BufferSelectEndPos.Column <= col + line.Length)
            {
                state.DisplaySelectEndPos.Row = currentDisplayLine;
                state.DisplaySelectEndPos.Column = state.BufferSelectEndPos.Column - col;
                return;
            }
            currentDisplayLine++;
        }
        state.DisplaySelectEndPos.Row = -1;
        state.DisplaySelectEndPos.Column = -1;
    }

    private static void UpdateCursorPosition(InputState state)
    {
        state.BufferPos.Row = state.DisplayLines[state.DisplayPos.Row].Row;
        state.BufferPos.Column = Math.Min(state.BufferLines[state.BufferPos.Row].Length,
            state.DisplayLines[state.DisplayPos.Row].Column + state.DisplayPos.Column);
    }

    private static void UpdateDisplayColumn(InputState state)
    {
        var lineLength = state.DisplayLines[state.DisplayPos.Row].Line.Length;

        if (state.DisplayPos.Column > lineLength)
        {
            state.DisplayPos.Column = lineLength;
        }
        else if (state.VerticalDisplayColumn > lineLength)
        {
            state.DisplayPos.Column = lineLength;
        }
        else
        {
            state.DisplayPos.Column = state.VerticalDisplayColumn;
        }
        UpdateCursorPosition(state);
    }
}