using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;

namespace Cute.Services.Markdown.Console.SyntaxHighlighters;

/// <summary>
/// Provides very basic syntax highlighting.  Recommend as a fallback option, should other
/// highlighters fail.
/// </summary>
public class BasicSyntaxHighlighter : ISyntaxHighlighter
{
    private const string EscapeCode = "\u001b";
    private const string Grey = "244";

    /// <inheritdoc/>
    public bool TryGetHighlightSyntax(
        string code,
        string? language,
        [NotNullWhen(returnValue: true)]
        out string? highlightedCode)
    {
        var lineNumber = 1;
        var lines = code.Replace("\r\n", string.Empty).Split('\n');

        highlightedCode = string.Empty;
        foreach (var line in lines)
        {
            var rightAlignedLineNumber = lineNumber++.ToString().PadLeft(4);
            highlightedCode += $"{EscapeCode}[38;5;{Grey}m{rightAlignedLineNumber} {EscapeCode}[0m{line}\n";
        }
        return true;
    }
}
