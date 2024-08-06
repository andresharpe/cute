namespace Cute.Lib.Contentful.BulkActions;

public class BulkActionResponse
{
    public BulkActionSys Sys { get; set; } = default!;
    public string Action { get; set; } = default!;

    public BulkActionError Error { get; set; } = default!;
}