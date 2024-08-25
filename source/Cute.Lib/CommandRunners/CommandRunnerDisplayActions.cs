namespace Cute.Lib.CommandRunners;

public class CommandRunnerDisplayActions
{
    public Action<string>? DisplayNormal { get; set; } = null!;
    public Action<FormattableString>? DisplayFormatted { get; set; } = null!;
    public Action<string>? DisplayAlert { get; set; } = null!;
    public Action<string>? DisplayDim { get; set; } = null!;
    public Action<string>? DisplayHeading { get; set; } = null!;
    public Action? DisplayRuler { get; set; } = null!;
    public Action? DisplayBlankLine { get; set; } = null!;
}