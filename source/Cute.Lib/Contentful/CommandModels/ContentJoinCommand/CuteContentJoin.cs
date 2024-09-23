using Contentful.Core;

namespace Cute.Lib.Contentful.CommandModels.ContentJoinCommand
{
    public class CuteContentJoin
    {
        public string Key { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string TargetContentType { get; set; } = default!;
        public string SourceContentType1 { get; set; } = default!;
        public List<string> SourceKeys1 { get; set; } = default!;
        public string SourceContentType2 { get; set; } = default!;
        public List<string> SourceKeys2 { get; set; } = default!;

        public static CuteContentJoin? GetByKey(ContentfulClient contentfulClient, string key)
        {
            return ContentfulEntryEnumerator
                .DeliveryEntries<CuteContentJoin>(
                    contentfulClient,
                    "cuteContentJoin",
                    pageSize: 1,
                    queryConfigurator: b => b.FieldEquals("fields.key", key)
                )
                .ToBlockingEnumerable()
                .Select(e => e.Entry)
                .FirstOrDefault();
        }
    }
}
