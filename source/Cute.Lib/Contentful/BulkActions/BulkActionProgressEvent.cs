namespace Cute.Lib.Contentful.BulkActions;

public struct BulkActionProgressEvent(int? step, int? steps, FormattableString? message, FormattableString? error)
{
    public int? Step { get; set; } = step;
    public int? Steps { get; set; } = steps;
    public FormattableString? Message { get; set; } = message;
    public FormattableString? Error { get; set; } = error;
}