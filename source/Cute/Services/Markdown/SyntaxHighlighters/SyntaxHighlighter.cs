namespace Cute.Services.Markdown.Console.SyntaxHighlighters;

public class SyntaxHighlighter
{
    // Order is important here.
    private ISyntaxHighlighter _highlighter = new BasicSyntaxHighlighter();

    /// <summary>
    /// Highlights syntax within a code block.
    /// </summary>
    /// <param name="code">Highlight the syntax within this code block</param>
    /// <param name="language">Hint provided to the syntax highlighter</param>
    /// <param name="forceBasicHighlighter">Ensure we use the basic highlighter.  Useful for test environments where Bat may not be installed.</param>
    /// <returns></returns>
    public string[] GetHighlightedSyntax(string code, string? language, bool forceBasicHighlighter)
    {
        if (_highlighter.TryGetHighlightSyntax(code, language, out var highlightedCode))
        {
            return highlightedCode;
        }

        throw new Exception("Syntax highlighting failed");
    }
}