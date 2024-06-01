using Cut.Constants;
using Spectre.Console;

namespace Cut.UiComponents;

internal static class ProgressBars
{
    public static Progress Instance()
    {
        return AnsiConsole.Progress()
            .HideCompleted(false)
            .AutoRefresh(true)
            .AutoClear(false)
            .Columns(
                [
                    new TaskDescriptionColumn()
                    {
                        Alignment = Justify.Left
                    },
                    new ProgressBarColumn()
                    {
                        CompletedStyle = Globals.StyleAlertAccent,
                        FinishedStyle = Globals.StyleAlertAccent,
                        IndeterminateStyle = Globals.StyleDim,
                        RemainingStyle = Globals.StyleDim,
                    },
                    new PercentageColumn()
                    {
                        CompletedStyle = Globals.StyleAlertAccent,
                        Style = Globals.StyleNormal,
                    },
                    new SpinnerColumn()
                    {
                        Style = Globals.StyleSubHeading,
                    },
                ]
            );
    }
}