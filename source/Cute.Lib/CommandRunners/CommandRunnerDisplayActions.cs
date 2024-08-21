namespace Cute.Lib.CommandRunners;

public class CommandRunnerDisplayActions
{
    public Action<string>? DisplayAction { get; set; } = null!;
    public Action<FormattableString>? DisplayFormattedAction { get; set; } = null!;
    public Action<string>? DisplayAlertAction { get; set; } = null!;
    public Action<string>? DisplayDimAction { get; set; } = null!;
    public Action? DisplayRuler { get; set; } = null!;
    public Action? DisplayBlankLine { get; set; } = null!;
}