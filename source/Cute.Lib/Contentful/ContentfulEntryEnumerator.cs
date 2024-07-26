using Contentful.Core.Models;
using Contentful.Core.Search;
using Newtonsoft.Json.Linq;
using Contentful.Core;

namespace Cute.Lib.Contentful;

public static class ContentfulEntryEnumerator
{
    public static async IAsyncEnumerable<(Entry<JObject>, ContentfulCollection<Entry<JObject>>)> Entries(ContentfulManagementClient client, string contentType, string orderByField,
        int includeLevels = 2, Action<QueryBuilder<Entry<JObject>>>? queryConfigurator = null)
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

            if (queryConfigurator is not null)
            {
                queryConfigurator(queryBuilder);
            }

            var entries = await client.GetEntriesCollection(queryBuilder);

            if (!entries.Any()) break;

            foreach (var entry in entries)
            {
                yield return (entry, entries);
            }

            skip += page;
        }
    }

    public static async IAsyncEnumerable<(T, ContentfulCollection<T>)> DeliveryEntries<T>(ContentfulClient client, string contentType, string orderByField,
        int includeLevels = 2, Action<QueryBuilder<T>>? queryConfigurator = null) where T : class, new()
    {
        var skip = 0;
        var page = 100;

        while (true)
        {
            var queryBuilder = new QueryBuilder<T>()
                .ContentTypeIs(contentType)
                .Include(includeLevels)
                .Skip(skip)
                .Limit(page)
                .OrderBy($"fields.{orderByField}");

            if (queryConfigurator is not null)
            {
                queryConfigurator(queryBuilder);
            }

            var entries = await client.GetEntries(queryBuilder);

            if (!entries.Any()) break;

            foreach (var entry in entries)
            {
                yield return (entry, entries);
            }

            skip += page;
        }
    }
}