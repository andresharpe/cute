using System.Text;

namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    private static void SelectAll(InputState state)
    {
        state.IsSelecting = true;
        state.BufferSelectStartPos.Row = 0;
        state.BufferSelectStartPos.Column = 0;
        state.BufferSelectEndPos.Row = state.BufferLines.Count - 1;
        state.BufferSelectEndPos.Column = state.BufferLines[state.BufferSelectEndPos.Row].Length;
    }

    private static void UpdateSelection(InputState state, bool isShiftPressed)
    {
        // Detect if the cursor moved backward (left or up)

        if (isShiftPressed)
        {
            bool movedBackward = state.BufferPos.Row < state.BufferPreviousPos.Row ||
                                 (state.BufferPos.Row == state.BufferPreviousPos.Row && state.BufferPos.Column < state.BufferPreviousPos.Column);

            if (!state.IsSelecting)
            {
                // Initialize selection: start at the previous position and end at the current
                state.IsSelecting = true;
                if (movedBackward)
                {
                    state.BufferSelectStartPos.Column = state.BufferPos.Column;
                    state.BufferSelectStartPos.Row = state.BufferPos.Row;
                    state.BufferSelectEndPos.Column = Math.Max(0, state.BufferPreviousPos.Column - 1);
                    state.BufferSelectEndPos.Row = state.BufferPreviousPos.Row;
                }
                else
                {
                    state.BufferSelectStartPos.Column = state.BufferPreviousPos.Column;
                    state.BufferSelectStartPos.Row = state.BufferPreviousPos.Row;
                    state.BufferSelectEndPos.Column = Math.Max(0, state.BufferPos.Column - 1);
                    state.BufferSelectEndPos.Row = state.BufferPos.Row;
                }
            }
            else
            {
                // If the user is moving left (or up), we adjust the selection start
                if (movedBackward)
                {
                    // If moving left and passing the original selection end, update start
                    if (state.BufferPos.Row < state.BufferSelectStartPos.Row ||
                        (state.BufferPos.Row == state.BufferSelectStartPos.Row && state.BufferPos.Column < state.BufferSelectStartPos.Column))
                    {
                        // Shrink the selection by moving the end towards the start
                        state.BufferSelectStartPos.Column = state.BufferPos.Column;
                        state.BufferSelectStartPos.Row = state.BufferPos.Row;
                    }
                    else if (state.BufferPos.Row < state.BufferSelectEndPos.Row ||
                        (state.BufferPos.Row == state.BufferSelectEndPos.Row && state.BufferPos.Column <= state.BufferSelectEndPos.Column))
                    {
                        // Shrink the selection by moving the end towards the start
                        state.BufferSelectEndPos.Column = Math.Max(0, state.BufferPos.Column - 1);
                        state.BufferSelectEndPos.Row = state.BufferPos.Row;
                    }
                    else
                    {
                        // Extend or shrink the selection by adjusting the start
                        state.BufferSelectStartPos.Column = state.BufferPos.Column;
                        state.BufferSelectStartPos.Row = state.BufferPos.Row;
                    }
                }
                else
                {
                    // If moving right (or down), adjust the selection end
                    if (state.BufferPos.Row > state.BufferSelectEndPos.Row ||
                        (state.BufferPos.Row == state.BufferSelectEndPos.Row && state.BufferPos.Column > state.BufferSelectEndPos.Column))
                    {
                        // Extend the selection by moving the start towards the end
                        state.BufferSelectEndPos.Column = Math.Max(0, state.BufferPos.Column - 1);
                        state.BufferSelectEndPos.Row = state.BufferPos.Row;
                    }
                    else if (state.BufferPos.Row > state.BufferSelectStartPos.Row ||
                        (state.BufferPos.Row == state.BufferSelectStartPos.Row && state.BufferPos.Column > state.BufferSelectStartPos.Column))
                    {
                        // Shrink the selection by moving the start towards the end
                        state.BufferSelectStartPos.Column = state.BufferPos.Column;
                        state.BufferSelectStartPos.Row = state.BufferPos.Row;
                    }
                    else
                    {
                        // Extend or shrink the selection by adjusting the end
                        state.BufferSelectEndPos.Column = Math.Max(0, state.BufferPos.Column - 1);
                        state.BufferSelectEndPos.Row = state.BufferPos.Row;
                    }
                }
            }
        }
        else
        {
            // Reset selection when Shift is released
            state.IsSelecting = false;
        }

        // Update previous cursor position for the next move
        state.BufferPreviousPos.Column = state.BufferPos.Column;
        state.BufferPreviousPos.Row = state.BufferPos.Row;
    }

    private static string GetSelectedText(InputState state)
    {
        if (state.BufferSelectStartPos.Row > state.BufferSelectEndPos.Row
            || (state.BufferSelectStartPos.Row == state.BufferSelectEndPos.Row && state.BufferSelectStartPos.Column > state.BufferSelectEndPos.Column))
        {
            var tempLine = state.BufferSelectStartPos.Row;
            var tempColumn = state.BufferSelectStartPos.Column;
            state.BufferSelectStartPos.Row = state.BufferSelectEndPos.Row;
            state.BufferSelectStartPos.Column = state.BufferSelectEndPos.Column;
            state.BufferSelectEndPos.Row = tempLine;
            state.BufferSelectEndPos.Column = tempColumn;
        }

        if (state.BufferSelectStartPos.Row == state.BufferSelectEndPos.Row)
        {
            return state.BufferLines[state.BufferSelectStartPos.Row].ToString(state.BufferSelectStartPos.Column, state.BufferSelectEndPos.Column - state.BufferSelectStartPos.Column);
        }
        else
        {
            var selectedLines = state.BufferLines.GetRange(state.BufferSelectStartPos.Row, state.BufferSelectEndPos.Row - state.BufferSelectStartPos.Row + 1);
            selectedLines[0] = new StringBuilder(selectedLines[0].ToString(state.BufferSelectStartPos.Column, selectedLines[0].Length - state.BufferSelectStartPos.Column));
            int lastIndex = selectedLines.Count - 1;
            selectedLines[lastIndex] = new StringBuilder(selectedLines[lastIndex].ToString(0, state.BufferSelectEndPos.Column));
            return string.Join("\n", selectedLines.Select(sb => sb.ToString()));
        }
    }
}