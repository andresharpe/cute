using Contentful.Core;

namespace Cute.Lib.Contentful.CommandModels.ContentSyncApi;

public class CuteContentSyncApi
{
    public string Key { get; set; } = default!;
    public int Order { get; set; } = default!;
    public string Yaml { get; set; } = default!;
    public string Schedule { get; set; } = default!;

    public static CuteContentSyncApi? GetByKey(ContentfulClient contentfulClient, string key)
    {
        return ContentfulEntryEnumerator
            .DeliveryEntries<CuteContentSyncApi>(
                contentfulClient,
                "cuteContentSyncApi",
                pageSize: 1,
                queryConfigurator: b => b.FieldEquals("fields.key", key)
            )
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .FirstOrDefault();
    }
}