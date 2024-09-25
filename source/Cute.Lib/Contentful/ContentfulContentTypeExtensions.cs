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
        ContentfulManagementClient client)
    {
        if (contentType is null)
        {
            return;
        }

        contentType.Name = contentType.Name
            .RemoveEmojis()
            .Trim();

        // Temp hack: Contentful API does not yet understand Taxonomy Tags

        contentType.Metadata = null;

        // end: hack

        contentType = await RateLimiter.SendRequestAsync(() => client.CreateOrUpdateContentType(contentType));

        await RateLimiter.SendRequestAsync(() => client.ActivateContentType(contentType.SystemProperties.Id, 1));
    }

    public static async Task<ContentType> CloneWithId(this ContentType contentType,
        ContentfulManagementClient client, string contentTypeId)
    {
        if (contentType is null)
        {
            return await Task.FromResult(new ContentType());
        }

        var clonedContentType = JObject.FromObject(contentType).DeepClone().ToObject<ContentType>();

        if (clonedContentType is null)
        {
            throw new ArgumentException("This should not occur. Cloning object using Newtonsoft.Json hack failed.");
        }

        clonedContentType.Name = clonedContentType.Name
            .RemoveEmojis()
            .Trim();

        if (clonedContentType.SystemProperties.Id != contentTypeId)
        {
            clonedContentType.SystemProperties.Id = contentTypeId;
            clonedContentType.Name = contentTypeId.CamelToPascalCase();
        }

        // Temp hack: Contentful API does not yet understand Taxonomy Tags

        clonedContentType.Metadata = null;

        // end: hack

        clonedContentType = await RateLimiter.SendRequestAsync(() => client.CreateOrUpdateContentType(clonedContentType));

        await RateLimiter.SendRequestAsync(() => client.ActivateContentType(contentTypeId, 1));

        return clonedContentType;
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