using System.Diagnostics.CodeAnalysis;

namespace Cute.Services.Markdown.Console.SyntaxHighlighters;

/// <summary>
/// Highlights syntax in a given code sample.
/// </summary>
public interface ISyntaxHighlighter
{
    /// <summary>
    /// Attempts to highlight syntax for a given code example.
    /// </summary>
    /// <param name="code"Code to highlight></param>
    /// <param name="language">Language is used to provide a hint to the highlighter</param>
    /// <param name="highlightedCode">Code with syntax highlighted</param>
    /// <returns></returns>
    public bool TryGetHighlightSyntax(
        string code,
        string? language,
        [NotNullWhen(returnValue: true)]
        out string[] highlightedCode);
}