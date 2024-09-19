﻿using Contentful.Core;
using Contentful.Core.Configuration;
using Contentful.Core.Extensions;
using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Lib.Extensions;
using Cute.Lib.RateLimiters;
using FuzzySharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

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

    public static async Task<ContentType> CreateWithId(this ContentType contentType,
        ContentfulManagementClient client, string contentTypeId)
    {
        if (contentType is null)
        {
            return await Task.FromResult(new ContentType());
        }

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

        contentType = await client.CreateOrUpdateContentType(contentType);

        await client.ActivateContentType(contentTypeId, 1);

        return contentType;
    }

    public static async Task<ContentType> CloneWithId(this ContentType contentType,
        ContentfulManagementClient client, string contentTypeId)
    {
        if (contentType is null)
        {
            return await Task.FromResult(new ContentType());
        }

        var contentTypeJson = contentType.ConvertObjectToJsonString();

        CamelCasePropertyNamesContractResolver camelCasePropertyNamesContractResolver = new CamelCasePropertyNamesContractResolver();
        camelCasePropertyNamesContractResolver.NamingStrategy!.OverrideSpecifiedNames = false;
        JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = camelCasePropertyNamesContractResolver
        };
        jsonSerializerSettings.Converters.Add(new ExtensionJsonConverter());
        var clonedContentType = JsonConvert.DeserializeObject<ContentType>(contentTypeJson, jsonSerializerSettings);

        clonedContentType!.Name = clonedContentType.Name
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

        clonedContentType = await client.CreateOrUpdateContentType(clonedContentType);

        await client.ActivateContentType(contentTypeId, 1);

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