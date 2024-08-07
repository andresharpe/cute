﻿using Contentful.Core;
using Contentful.Core.Models;
using Contentful.Core.Search;
using System.Text;

namespace Cute.Lib.Contentful;

public static class ContentfulEntryEnumerator
{
    public static async IAsyncEnumerable<(T Entry, ContentfulCollection<T> Entries)> Entries<T>(
        ContentfulManagementClient client, string contentType,
        string? orderByField = null,
        int includeLevels = 2, Action<QueryBuilder<T>>? queryConfigurator = null, string? queryString = null, int pageSize = 1000)
    {
        var skip = 0;

        while (true)
        {
            var queryBuilder = new QueryBuilder<T>()
                .ContentTypeIs(contentType)
                .Include(includeLevels)
                .Skip(skip)
                .Limit(pageSize);

            if (orderByField != null)
            {
                queryBuilder.OrderBy($"fields.{orderByField}");
            }

            if (queryConfigurator is not null)
            {
                queryConfigurator(queryBuilder);
            }

            var fullQueryString = new StringBuilder(queryBuilder.Build());

            if (queryString != null)
            {
                fullQueryString.Append('&');
                fullQueryString.Append(queryString);
            }

            var entries = await client.GetEntriesCollection<T>(fullQueryString.ToString());

            if (!entries.Any()) break;

            foreach (var entry in entries)
            {
                yield return (Entry: entry, Entries: entries);
            }

            skip += pageSize;
        }
    }

    public static async IAsyncEnumerable<(T Entry, ContentfulCollection<T> Entries)> DeliveryEntries<T>(ContentfulClient client,
        string contentType, string? orderByField = null,
        int includeLevels = 2, Action<QueryBuilder<T>>? queryConfigurator = null) where T : class, new()
    {
        var skip = 0;
        var page = 1000;

        while (true)
        {
            var queryBuilder = new QueryBuilder<T>()
                .ContentTypeIs(contentType)
                .Include(includeLevels)
                .Skip(skip)
                .Limit(page);

            if (orderByField != null)
            {
                queryBuilder.OrderBy($"fields.{orderByField}");
            }

            if (queryConfigurator is not null)
            {
                queryConfigurator(queryBuilder);
            }

            var entries = await client.GetEntries(queryBuilder);

            if (!entries.Any()) break;

            foreach (var entry in entries)
            {
                yield return (Entry: entry, Entries: entries);
            }

            skip += page;
        }
    }
}