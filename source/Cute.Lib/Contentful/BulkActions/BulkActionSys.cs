namespace Cute.Lib.Contentful.BulkActions;

public class BulkActionSys
{
    public string Type { get; set; } = default!;
    public string Id { get; set; } = default!;
    public string SchemaVersion { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = default!;
    public DateTime UpdatedAt { get; set; } = default!;
    public string Status { get; set; } = default!;
}