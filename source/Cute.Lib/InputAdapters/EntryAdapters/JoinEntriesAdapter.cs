using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Contentful.Core.Search;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.CommandModels.ContentJoinCommand;
using Cute.Lib.Exceptions;
using Cute.Lib.Serializers;
using DocumentFormat.OpenXml.Math;
using Newtonsoft.Json.Linq;
using System;

namespace Cute.Lib.InputAdapters.EntryAdapters;

public class JoinEntriesAdapter(CuteContentJoin cuteContentJoin, ContentfulConnection contentfulConnection,
    ContentLocales contentLocales, ContentType sourceContentType1, ContentType sourceContentType2, ContentType targetContentType, string? source2EntryId)
    : InputAdapterBase(nameof(JoinEntriesAdapter))
{
    private List<Dictionary<string, object?>> _results = default!;

    private EntrySerializer _serializer = default!;

    private int _currentRecordIndex = -1;

    private readonly CuteContentJoin _cuteContentJoin = cuteContentJoin;

    private readonly ContentfulConnection _contentfulConnection = contentfulConnection;

    private readonly ContentLocales _contentLocales = contentLocales;
    private readonly ContentType _sourceContentType1 = sourceContentType1;
    private readonly ContentType _sourceContentType2 = sourceContentType2;
    private readonly ContentType _targetContentType = targetContentType;
    private readonly string? _source2EntryId = source2EntryId;

    public override async Task<int> GetRecordCountAsync()
    {
        if (_results != null)
        {
            _currentRecordIndex = 0;
            return _results.Count;
        }

        var targetField1 = _targetContentType.Fields
            .Where(f => f.Validations.Any(v => v is LinkContentTypeValidator vLink && vLink.ContentTypeIds.Contains(_cuteContentJoin.SourceContentType1)))
            .FirstOrDefault()
            ?? throw new CliException($"No reference field for content type '{_cuteContentJoin.SourceContentType1}' found in '{_cuteContentJoin.TargetContentType}'");

        var targetField2 = _targetContentType.Fields
            .Where(f => f.Validations.Any(v => v is LinkContentTypeValidator vLink && vLink.ContentTypeIds.Contains(_cuteContentJoin.SourceContentType2)))
            .FirstOrDefault()
            ?? throw new CliException($"No reference field for content type '{_cuteContentJoin.SourceContentType2}' found in '{_cuteContentJoin.TargetContentType}'");

        var source1Keys = _cuteContentJoin.SourceKeys1.Select(k => k?.Trim()).ToHashSet();
        var source1AllKeys = source1Keys.Any(k => k == "*");
        var source2Keys = _cuteContentJoin.SourceKeys2.Select(k => k?.Trim()).ToHashSet();
        var source2AllKeys = source2Keys.Any(k => k == "*");

        Action<QueryBuilder<Entry<JObject>>> source1Filter = b =>
        {
            if (!source1AllKeys)
            {
                b.FieldIncludes("fields.key", source1Keys);
            }
        };

        Action<QueryBuilder<Entry<JObject>>> source2Filter = b =>
        {
            if (!source2AllKeys)
            {
                b.FieldIncludes("fields.key", source2Keys);
            }
            if (_source2EntryId != null)
            {
                b.FieldEquals("sys.id", _source2EntryId);
            }
        };

        var entries1 = ContentfulEntryEnumerator.Entries<Entry<JObject>>(_contentfulConnection.ManagementClient, _sourceContentType1.SystemProperties.Id, queryConfigurator: source1Filter)
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            //.Where(e => source1AllKeys || source1Keys.Contains(e.Fields["key"]?.Value<string>()))
            .ToList();

        var entries2 = ContentfulEntryEnumerator.Entries<Entry<JObject>>(_contentfulConnection.ManagementClient, _sourceContentType2.SystemProperties.Id, queryConfigurator: source2Filter)
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            //.Where(e => source2AllKeys || source2Keys.Contains(e.Fields["key"]?.Value<string>()))
            .ToList();

        await Task.Delay(0);

        var defaultLocale = _contentLocales.DefaultLocale;

        var targetSerializer = new EntrySerializer(_targetContentType, new ContentLocales([], defaultLocale));

        _results = [];

        var totalCount = entries1.Count * entries2.Count;

        foreach (var entry1 in entries1)
        {
            foreach (var entry2 in entries2)
            {
                var joinKey = $"{entry1.Fields.SelectToken($"key.{defaultLocale}")?.Value<string>()}.{entry2.Fields.SelectToken($"key.{defaultLocale}")?.Value<string>()}";
                var joinTitle = $"{entry1.Fields.SelectToken($"title.{defaultLocale}")?.Value<string>()} | {entry2.Fields.SelectToken($"title.{defaultLocale}")?.Value<string>()}";
                var joinName = $"{entry2.Fields.SelectToken($"name.{defaultLocale}")?.Value<string>()}";

                var newFlatEntry = targetSerializer.CreateNewFlatEntry();
                newFlatEntry[$"key.{defaultLocale}"] = joinKey;
                newFlatEntry[$"title.{defaultLocale}"] = joinTitle;
                newFlatEntry[$"name.{defaultLocale}"] = joinName;
                newFlatEntry[$"{targetField1.Id}.{defaultLocale}"] = entry1.SystemProperties.Id;
                newFlatEntry[$"{targetField2.Id}.{defaultLocale}"] = entry2.SystemProperties.Id;

                _results.Add(newFlatEntry);
                CountProgressNotifier?.Invoke(_results.Count, totalCount, $"Generating joined entry {_results.Count}/{totalCount}");
            }
        }

        return _results.Count;
    }

    public override async Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        if (_currentRecordIndex == -1)
        {
            await GetRecordCountAsync();
        }

        if (_currentRecordIndex >= _results.Count)
        {
            return null;
        }

        var result = _results[_currentRecordIndex];

        _currentRecordIndex++;

        return result;
    }
}