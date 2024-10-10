namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    public static IEnumerable<string> GetFixedLines(this ReadOnlySpan<char> input, int maxLength = 80, int? maxFirstLineLength = null)
    {
        var lines = new List<string>();

        if (maxFirstLineLength is not null && input.Length > maxFirstLineLength)
        {
            var length = Math.Min(maxFirstLineLength.Value, input.Length);
            var slice = input[..length];
            var lastBreakIndex = slice.LastIndexOfAny(' ', '\n', '\r');
            if (lastBreakIndex == -1)
            {
                lines.Add(string.Empty);
                maxFirstLineLength = maxLength;
            }
        }

        maxFirstLineLength ??= maxLength;

        while (!input.IsEmpty)
        {
            int maxLineLength = lines.Count == 0
                ? maxFirstLineLength.Value
                : maxLength;

            // Find the maximum slice we can take for this line
            var length = Math.Min(maxLineLength, input.Length);
            var slice = input[..length]; // Using the range operator here for slicing

            // Find the first occurrence of \r or \n
            var firstNewlineIndex = slice.IndexOfAny('\r', '\n');
            // Find the last occurrence of a space
            var lastSpaceIndex = slice.LastIndexOf(' ');

            if (lastSpaceIndex != -1 && firstNewlineIndex > lastSpaceIndex)
            {
                lastSpaceIndex = -1;
            }

            if (lastSpaceIndex != -1 && length < maxLineLength)
            {
                lastSpaceIndex = -1;
            }

            // Break at the first newline character
            if (firstNewlineIndex != -1 && (firstNewlineIndex < lastSpaceIndex || lastSpaceIndex == -1))
            {
                lines.Add(slice[..firstNewlineIndex].ToString());

                // Handle \r\n as a single line break
                if (firstNewlineIndex + 1 < input.Length && input[firstNewlineIndex] == '\r' && input[firstNewlineIndex + 1] == '\n')
                    input = input[(firstNewlineIndex + 2)..]; // Skip \r\n using range
                else
                    input = input[(firstNewlineIndex + 1)..]; // Skip \r or \n using range

                continue;
            }

            // Break at the last space if no newline is found earlier
            if (lastSpaceIndex != -1)
            {
                lines.Add(slice[..lastSpaceIndex].ToString());
                input = input[(lastSpaceIndex + 1)..]; // Skip the space using range
                continue;
            }

            // If no space or newline was found, just break at max length
            lines.Add(slice.ToString());
            input = input[length..]; // Move to the next chunk using range operator
        }

        return lines;
    }
}