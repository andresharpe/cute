namespace Cute.Lib.InputAdapters.Http.Models;

public class ContentEntryDefinition
{
    public string ContentType { get; set; } = default!;

    public string QueryParameters { get; set; } = default!;
    public string Filter { get; set; } = default!;
}