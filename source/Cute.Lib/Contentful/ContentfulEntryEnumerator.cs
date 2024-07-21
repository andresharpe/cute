using Contentful.Core.Models;
using Contentful.Core.Search;
using Newtonsoft.Json.Linq;
using Contentful.Core;

namespace Cute.Lib.Contentful;

public static class ContentfulEntryEnumerator
{
    public static async IAsyncEnumerable<(Entry<JObject>, ContentfulCollection<Entry<JObject>>)> Entries(ContentfulManagementClient client, string contentType, string orderByField, int includeLevels = 2)
    {
        var skip = 0;
        var page = 100;

        while (true)
        {
            var queryBuilder = new QueryBuilder<Entry<JObject>>()
                .ContentTypeIs(contentType)
                .Include(includeLevels)
                .Skip(skip)
                .Limit(page)
                .OrderBy($"fields.{orderByField}");

            var query = queryBuilder.Build();

            var entries = await client.GetEntriesCollection<Entry<JObject>>(query);

            if (!entries.Any()) break;

            foreach (var entry in entries)
            {
                yield return (entry, entries);
            }

            skip += page;
        }
    }
}