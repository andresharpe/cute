using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Spectre.Console;

namespace Cute.Services.Markdown.Console.SyntaxHighlighters;

/// <summary>
/// A wrapper around Bat.  A cli tool that provides syntax highlighting.
///
/// <remarks>
/// Requires Bat.exe to available in the current working directory or via the path
/// environmental variable.
/// </remarks>
///
/// <seealso href="https://github.com/sharkdp/bat">Bat/seealso>
/// </summary>
public class BatSyntaxHighlighter : ISyntaxHighlighter
{
    /// <inheritdoc/>
    public bool TryGetHighlightSyntax(
        string code,
        string? language,
        [NotNullWhen(returnValue: true)]
        out string? highlightedCode)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "bat.exe",
                // We cannot use ArgumentList here, because .NetStandard2.0 does not support it.
                Arguments = GetBatArguments(language),
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };
            var process = Process.Start(info) ?? throw new Exception("Cannot start Bat");
            var writer = process.StandardInput;
            var output = process.StandardOutput;

            writer.WriteLine(code);
            writer.Close();
            process.WaitForExit();

            highlightedCode = output.ReadToEnd();
            return true;
        }
        catch
        {
            highlightedCode = null;
            return false;
        }
    }

    private string GetBatArguments(string? language)
    {
        var arguments = new StringBuilder();

        arguments.Append("--number");
        arguments.Append("--color always");
        arguments.Append($"--terminal-width ${System.Console.BufferWidth}");
        // Can be langague name or common file extension.
        // See Bat --list-languages for support language codes.
        // This can throw, which will result in falling back to the basic syntax highlighter.
        arguments.Append($"--language {language ?? "unknown"}");

        return arguments.ToString();
    }
}
