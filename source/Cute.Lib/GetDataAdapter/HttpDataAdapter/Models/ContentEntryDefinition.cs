namespace Cute.Lib.GetDataAdapter;

public class ContentEntryDefinition
{
    public string ContentType { get; set; } = default!;

    public string QueryParameters { get; set; } = default!;
    public string Filter { get; set; } = default!;
}