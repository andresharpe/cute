namespace Cute.Services.ReadLine;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class StringLinesBuilder : IEnumerable<string>
{
    private readonly StringBuilder _buffer;
    private readonly List<int> _lineOffsets; // Stores the start index of each line
    private bool _isDirty;

    public StringLinesBuilder(string initialContent = "")
    {
        _buffer = new();
        _lineOffsets = [];
        Append(initialContent);
    }

    // Create a new StringBuffer from existing without sharing state
    public StringLinesBuilder(StringLinesBuilder other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _buffer = new StringBuilder(other._buffer.ToString());
        _lineOffsets = new List<int>(other._lineOffsets);
        _isDirty = other._isDirty;
    }

    // Indexer to access lines via [] syntax
    public ReadOnlyMemory<char> this[int lineIndex]
    {
        get
        {
            return GetLineSpan(lineIndex);
        }
    }

    // Properties
    public int Count
    {
        get
        {
            EnsureOffsetsAreValid();
            return _lineOffsets.Count;
        }
    }

    // Access a line by its index as a string
    public string GetLine(int lineIndex)
    {
        EnsureOffsetsAreValid();
        if (lineIndex < 0 || lineIndex >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        int start = _lineOffsets[lineIndex];
        int end = (lineIndex + 1 < _lineOffsets.Count) ? _lineOffsets[lineIndex + 1] - 1 : _buffer.Length;

        // Return the line without the newline character
        return _buffer.ToString(start, end - start);
    }

    public ReadOnlyMemory<char> GetLineSpan(int lineIndex, bool includeNewline = false)
    {
        EnsureOffsetsAreValid();

        if (lineIndex < 0 || lineIndex >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        int newlineOffset = includeNewline ? 0 : 1;
        int start = _lineOffsets[lineIndex];
        int end = (lineIndex + 1 < _lineOffsets.Count) ? _lineOffsets[lineIndex + 1] - newlineOffset : _buffer.Length;
        int length = end - start;

        // Use a shared buffer or allocate only the required memory for the line span
        char[] lineChars = new char[length];
        _buffer.CopyTo(start, lineChars, 0, length);

        return new ReadOnlyMemory<char>(lineChars);
    }

    // Get a range of text including newlines to support undo or other operations
    public string GetRange(int startRow, int startColumn, int endRow, int endColumn)
    {
        EnsureOffsetsAreValid();

        if (startRow < 0 || startRow >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(startRow), "Row index is out of range.");

        if (endRow < 0 || endRow >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(endRow), "Row index is out of range.");

        if (startRow > endRow || (startRow == endRow && startColumn > endColumn))
            throw new ArgumentException("Invalid range specified.");

        // Get the starting and ending indices in the buffer
        int startIndex = _lineOffsets[startRow] + startColumn;
        int endIndex = _lineOffsets[endRow] + endColumn;

        // Ensure indices are within bounds
        if (startColumn < 0 || startColumn > GetLineLength(startRow))
            throw new ArgumentOutOfRangeException(nameof(startColumn), "Column index is out of range.");

        if (endColumn < 0 || endColumn > GetLineLength(endRow))
            throw new ArgumentOutOfRangeException(nameof(endColumn), "Column index is out of range.");

        // Extract the range including newlines
        return _buffer.ToString(startIndex, Math.Min(_buffer.Length, endIndex - startIndex + 1));
    }

    // Insert a string at a specific position (column and line)
    public int Insert(int lineIndex, int column, string text)
    {
        EnsureOffsetsAreValid();
        if (lineIndex < 0 || lineIndex >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        // Clamp the column to the length of the line
        int lineLength = GetLineLength(lineIndex);
        if (column > lineLength) column = lineLength;

        int insertIndex = _lineOffsets[lineIndex] + column;
        var normalizedText = NormalizeNewlines(text);
        _buffer.Insert(insertIndex, normalizedText);
        _isDirty = true;
        return normalizedText.Length;
    }

    // Overload for inserting a single character at a specific position
    public void Insert(int lineIndex, int column, char character)
    {
        EnsureOffsetsAreValid();
        if (lineIndex < 0 || lineIndex >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        // Clamp the column to the length of the line
        int lineLength = GetLineLength(lineIndex);
        if (column > lineLength) column = lineLength;

        int insertIndex = _lineOffsets[lineIndex] + column;
        _buffer.Insert(insertIndex, character);
        _isDirty = true;
    }

    // Insert multiple lines from a string array at a specific line index
    public void InsertLines(int lineIndex, string[] lines)
    {
        EnsureOffsetsAreValid();

        if (lineIndex < 0 || lineIndex > _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex), "Line index is out of range.");

        if (lines == null || lines.Length == 0)
            throw new ArgumentException("Lines array must not be null or empty.", nameof(lines));

        // Normalize each line and prepare a complete string to insert
        StringBuilder textToInsert = new();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                textToInsert.Append('\n'); // Add a newline character between lines
            }
            textToInsert.Append(NormalizeNewlines(lines[i]));
        }

        // Determine the index in the buffer where the text should be inserted
        int insertIndex = (lineIndex < _lineOffsets.Count) ? _lineOffsets[lineIndex] : _buffer.Length;

        // Insert the text at the specified position
        _buffer.Insert(insertIndex, textToInsert.ToString());
        _isDirty = true; // Mark offsets as invalid to trigger a recalculation
    }

    public (int row, int column) GetLastPositionAfterInsert(int lineIndex, int column, string text)
    {
        EnsureOffsetsAreValid();

        if (lineIndex < 0 || lineIndex >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        // Normalize the text (this matches how the Insert method processes the text)
        string normalizedText = NormalizeNewlines(text);

        // Start with the given row and column
        int currentRow = lineIndex;
        int currentColumn = column;

        // Traverse through the normalized text to calculate the final row and column
        foreach (char c in normalizedText)
        {
            if (c == '\n')
            {
                currentRow++;
                currentColumn = 0; // Reset column after new line
            }
            else
            {
                currentColumn++;
            }
        }

        // Ensure the row index is valid
        if (currentRow > _lineOffsets.Count)
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
            throw new ArgumentOutOfRangeException("The final row index after the insert exceeds the number of lines in the buffer.");
#pragma warning restore CA2208 // Instantiate argument exceptions correctly

        return (currentRow, currentColumn);
    }

    // Remove a specific range of text from the buffer (line-based)
    public void Remove(int lineIndex, int column, int length)
    {
        EnsureOffsetsAreValid();
        if (lineIndex < 0 || lineIndex >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        int deleteIndex = _lineOffsets[lineIndex] + column;
        _buffer.Remove(deleteIndex, length);
        _isDirty = true;
    }

    // Overload to remove text across multiple lines
    public void Remove(int startRow, int startColumn, int endRow, int endColumn)
    {
        EnsureOffsetsAreValid();

        if (startRow < 0 || startRow >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(startRow));

        if (endRow < 0 || endRow >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(endRow));

        if (startRow > endRow || (startRow == endRow && startColumn > endColumn))
            throw new ArgumentException("Invalid range specified.");

        // Get the starting and ending indices in the buffer
        int startIndex = _lineOffsets[startRow] + startColumn;
        int endIndex = _lineOffsets[endRow] + endColumn;

        // Ensure indices are within bounds
        if (startColumn < 0 || startColumn > GetLineLength(startRow))
            throw new ArgumentOutOfRangeException(nameof(startColumn));

        if (endColumn < 0 || endColumn > GetLineLength(endRow))
            throw new ArgumentOutOfRangeException(nameof(endColumn));

        int lengthToRemove = endIndex - startIndex + 1;
        _buffer.Remove(startIndex, Math.Min(_buffer.Length, lengthToRemove));
        _isDirty = true;
    }

    // Remove multiple lines between startLine and endLine (inclusive)
    public void RemoveLines(int startLine, int endLine)
    {
        EnsureOffsetsAreValid();

        if (startLine < 0 || startLine >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(startLine), "Start line index is out of range.");

        if (endLine < 0 || endLine >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(endLine), "End line index is out of range.");

        if (startLine > endLine)
            throw new ArgumentException("Start line must be less than or equal to end line.");

        // Determine the start index for the removal
        int startIndex = _lineOffsets[startLine];

        // Determine the end index for the removal
        int endIndex = (endLine + 1 < _lineOffsets.Count) ? _lineOffsets[endLine + 1] : _buffer.Length;

        // Remove the range from the buffer
        _buffer.Remove(startIndex, endIndex - startIndex);
        _isDirty = true; // Mark offsets as invalid to trigger a recalculation
    }

    // Append a string at the end of the buffer
    public void Append(string text)
    {
        string normalizedText = NormalizeNewlines(text);
        _buffer.Append(normalizedText);
        _isDirty = true;
    }

    // Remove an entire line by its index
    public void RemoveLine(int lineIndex)
    {
        EnsureOffsetsAreValid();

        if (lineIndex < 0 || lineIndex >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        int start = _lineOffsets[lineIndex];
        int end = (lineIndex + 1 < _lineOffsets.Count) ? _lineOffsets[lineIndex + 1] : _buffer.Length;

        // Remove the line including the newline character
        _buffer.Remove(start, end - start);
        _isDirty = true;
    }

    // Normalize newlines (\r\n -> \n)
    private static string NormalizeNewlines(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new StringBuilder(input.Length);

        for (int i = 0; i < input.Length; i++)
        {
            // If we encounter \r, check if it is followed by \n
            if (input[i] == '\r')
            {
                if (i + 1 < input.Length && input[i + 1] == '\n')
                {
                    // It's a \r\n sequence, add just \n and skip the next character
                    result.Append('\n');
                    i++; // Skip the next character
                }
                else
                {
                    // Standalone \r, convert to \n
                    result.Append('\n');
                }
            }
            else
            {
                // Normal character, just append it
                result.Append(input[i]);
            }
        }

        return result.ToString();
    }

    // Rebuilds the line offsets when the buffer changes
    private void RebuildLineOffsets()
    {
        _lineOffsets.Clear();
        _lineOffsets.Add(0); // The first line always starts at index 0

        for (int i = 0; i < _buffer.Length; i++)
        {
            if (_buffer[i] == '\n')
                _lineOffsets.Add(i + 1);
        }

        _isDirty = false;
    }

    // Helper to ensure line offsets are up to date
    private void EnsureOffsetsAreValid()
    {
        if (_isDirty)
            RebuildLineOffsets();
    }

    // Retrieve all lines
    public List<string> GetAllLines()
    {
        EnsureOffsetsAreValid();
        var lines = new List<string>();

        for (int i = 0; i < _lineOffsets.Count; i++)
        {
            lines.Add(GetLine(i));
        }

        return lines;
    }

    // Retrieve all lines
    public List<ReadOnlyMemory<char>> GetAllSpanLines()
    {
        EnsureOffsetsAreValid();
        var lines = new List<ReadOnlyMemory<char>>();

        for (int i = 0; i < _lineOffsets.Count; i++)
        {
            lines.Add(GetLineSpan(i));
        }

        return lines;
    }

    // Get the length of a line excluding the newline character
    private int GetLineLength(int lineIndex)
    {
        EnsureOffsetsAreValid();
        if (lineIndex < 0 || lineIndex >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        int start = _lineOffsets[lineIndex];
        int end = (lineIndex + 1 < _lineOffsets.Count) ? _lineOffsets[lineIndex + 1] - 1 : _buffer.Length;

        return end - start;
    }

    // IEnumerable implementation to enumerate lines
    public IEnumerator<string> GetEnumerator()
    {
        EnsureOffsetsAreValid();

        for (int i = 0; i < _lineOffsets.Count; i++)
        {
            yield return GetLine(i);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}