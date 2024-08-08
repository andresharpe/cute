namespace Cute.Lib.Contentful.BulkActions;

public class Sys
{
    public string Id { get; set; } = default!;
    public DateTime? PublishedAt { get; set; } = default!;
    public int? Version { get; set; } = default!;
    public int? PublishedVersion { get; set; }
    public int? ArchivedVersion { get; set; }
    public string DisplayFieldValue { get; internal set; } = default!;

    public bool IsDraft()
    {
        return PublishedVersion is null || PublishedVersion == 0;
    }

    public bool IsChanged()
    {
        return PublishedVersion != null && Version >= PublishedVersion + 2;
    }

    public bool IsPublished()
    {
        return PublishedVersion != null && Version == PublishedVersion + 1;
    }

    public bool IsArchived()
    {
        return ArchivedVersion != null;
    }
}