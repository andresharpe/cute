using System.Text;

namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static void PasteFromClipboard(InputState state)
    {
        var clipboardText = state.Clipboard.GetText() ?? "";
        var clipboardLines = clipboardText.Replace("\r\n", "\n").Split('\n');

        SaveUndoState(state);

        if (state.IsSelecting)
        {
            DeleteSelectedText(state);
            state.IsSelecting = false;
        }

        state.BufferLines[state.BufferPos.Row].Insert(state.BufferPos.Column, clipboardLines[0]);

        if (clipboardLines.Length > 1)
        {
            var remaining = new StringBuilder(state.BufferLines[state.BufferPos.Row].ToString(state.BufferPos.Column + clipboardLines[0].Length,
                state.BufferLines[state.BufferPos.Row].Length - (state.BufferPos.Column + clipboardLines[0].Length)));

            state.BufferLines[state.BufferPos.Row].Length = state.BufferPos.Column + clipboardLines[0].Length;

            state.BufferLines.InsertRange(state.BufferPos.Row + 1, clipboardLines.Skip(1).Select(l => new StringBuilder(l)));

            state.BufferLines.Insert(state.BufferPos.Row + clipboardLines.Length, remaining);
        }

        state.BufferPos.Column += clipboardLines[^1].Length;
        state.BufferPos.Row += clipboardLines.Length - 1;
        state.IsDisplayValid = false;
    }

    private static void CutToClipboard(InputState state)
    {
        if (state.IsSelecting)
        {
            var selectedText = GetSelectedText(state);
            state.Clipboard.SetText(selectedText);
            SaveUndoState(state);
            DeleteSelectedText(state);
            state.IsSelecting = false;
            state.IsDisplayValid = false;
        }
    }

    private static void CopyToClipboard(InputState state)
    {
        if (state.IsSelecting)
        {
            var selectedText = GetSelectedText(state);
            state.Clipboard.SetText(selectedText);
        }
        else
        {
            state.Clipboard.SetText(string.Join("\n", state.BufferLines));
        }
    }
}