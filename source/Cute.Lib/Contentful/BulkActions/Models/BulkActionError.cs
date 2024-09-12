namespace Cute.Lib.Contentful.BulkActions.Models;

public class BulkActionError
{
    public BulkActionErrorSys Sys { get; set; } = default!;
    public string Message { get; set; } = default!;
}