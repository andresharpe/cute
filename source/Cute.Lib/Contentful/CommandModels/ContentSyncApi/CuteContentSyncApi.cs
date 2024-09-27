namespace Cute.Lib.Contentful.CommandModels.ContentSyncApi;

public class CuteContentSyncApi
{
    public string Key { get; set; } = default!;
    public int Order { get; set; } = default!;
    public string Yaml { get; set; } = default!;
    public string Schedule { get; set; } = default!;

    public static CuteContentSyncApi? GetByKey(ContentfulConnection contentfulConnection, string key)
    {
        return contentfulConnection
            .GetPreviewEntryByKey<CuteContentSyncApi>("cuteContentSyncApi", "fields.key", key);
    }
}