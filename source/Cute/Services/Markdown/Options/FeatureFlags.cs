namespace Cute.Services.Markdown.Console.Options;

/// <summary>
/// Flags that modify the behaviour of the AnsiRenderer.
/// </summary>
internal static class FeatureFlags
{
    private const string ThrowOnSupportedEnvironmentVariable = "MORELLO_MARKDOWN_CONSOLE_THROW_ON_UNSUPPORTED_TYPE";
    private const string ForceBasicSyntaxHighlighterEnvironmentVariable = "MORELLO_MARKDOWN_CONSOLE_FORCE_BASIC_SYNTAX_HIGHLIGHTER";
    private const string ForceAnsiColourEnvironmentVariable = "MORELLO_MARKDOWN_CONSOLE_FORCE_ANSI_COLOUR";
    private const string True = "true";
    private const string Yes = "yes";
    private const string On = "on";
    private const string Enabled = "enabled";
    private const string Active = "active";

    /// <summary>
    /// Disables MarkdownConsole's default behaviour of falling back to plain text when it encounters
    /// an unsupported Markdown type.
    /// </summary>
    public static bool ThrowOnUnsupportedMarkdownType =>
        EnvironmentVariablePresentAndActive(ThrowOnSupportedEnvironmentVariable);

    public static bool ForceBasicSyntaxHighlighter =>
        EnvironmentVariablePresentAndActive(ForceBasicSyntaxHighlighterEnvironmentVariable);

    public static bool ForceAnsiColour =>
        EnvironmentVariablePresentAndActive(ForceAnsiColourEnvironmentVariable);

    private static bool EnvironmentVariablePresentAndActive(string environmentVariableName)
    {
        var value = Environment
            .GetEnvironmentVariable(environmentVariableName)?
            .ToLower()
            ?? string.Empty;

        return value is True or Yes or On or Enabled or Active;
    }
}
