namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static void PasteFromClipboard(InputState state)
    {
        var clipboardText = state.Clipboard.GetText() ?? "";

        if (state.IsSelecting)
        {
            DeleteSelectedText(state);
            state.IsSelecting = false;
        }

        InsertLines(state, clipboardText);

        state.IsDisplayValid = false;
    }

    private static void CutToClipboard(InputState state)
    {
        if (state.IsSelecting)
        {
            var selectedText = GetSelectedText(state);
            state.Clipboard.SetText(selectedText);
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