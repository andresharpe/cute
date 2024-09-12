using Contentful.Core;
using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Lib.Extensions;
using Cute.Lib.RateLimiters;
using FuzzySharp;
using Newtonsoft.Json.Linq;

namespace Cute.Lib.Contentful;

public static class ContentfulContentTypeExtensions
{
    public static string BestFieldMatch(this ContentType contentType, string input)
    {
        return contentType.Fields
            .OrderByDescending(f =>
                Fuzz.PartialRatio(input, f.Id)
            )
            .First().Id;
    }

    public static async Task CreateWithId(this ContentType contentType,
        ContentfulManagementClient client, string contentTypeId)
    {
        if (contentType is null) return;

        contentType.Name = contentType.Name
            .RemoveEmojis()
            .Trim();

        if (contentType.SystemProperties.Id != contentTypeId)
        {
            contentType.SystemProperties.Id = contentTypeId;
            contentType.Name = contentTypeId.CamelToPascalCase();
        }

        // Temp hack: Contentful API does not yet understand Taxonomy Tags

        contentType.Metadata = null;

        // end: hack

        await client.CreateOrUpdateContentType(contentType);

        await client.ActivateContentType(contentTypeId, 1);
    }

    public static IDictionary<string, int> TotalEntries(this IEnumerable<ContentType> contentTypes,
        ContentfulManagementClient client)
    {
        var tasks = contentTypes
            .Select(ct =>
            {
                var queryBuilder = new QueryBuilder<Entry<JObject>>()
                    .ContentTypeIs(ct.SystemProperties.Id)
                    .Include(0)
                    .Limit(0);

                return RateLimiter.SendRequestAsync(() => client.GetEntriesCollection(queryBuilder));
            })
            .ToArray();

        var tasksNames = contentTypes
            .Select(ct => ct.SystemProperties.Id)
            .ToArray();

        Task.WhenAll(tasks);

        var result = new Dictionary<string, int>();
        for (var i = 0; i < tasks.Length; i++)
        {
            var task = tasks[i];
            var name = tasksNames[i];
            result.Add(name, task.Result.Total);
        }

        return result;
    }
}