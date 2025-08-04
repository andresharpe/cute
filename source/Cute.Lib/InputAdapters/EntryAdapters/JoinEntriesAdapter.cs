using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Contentful.Core.Search;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.CommandModels.ContentJoinCommand;
using Cute.Lib.Exceptions;
using Cute.Lib.Serializers;
using Newtonsoft.Json.Linq;

namespace Cute.Lib.InputAdapters.EntryAdapters;

public class JoinEntriesAdapter(CuteContentJoin cuteContentJoin, ContentfulConnection contentfulConnection,
    ContentLocales contentLocales, ContentType sourceContentType1, ContentType sourceContentType2, ContentType? sourceContentType3, ContentType targetContentType, string? source2EntryId)
    : InputAdapterBase(nameof(JoinEntriesAdapter))
{
    private List<Dictionary<string, object?>> _results = default!;

    // removed
    private int _currentRecordIndex = -1;

    private readonly CuteContentJoin _cuteContentJoin = cuteContentJoin;

    private readonly ContentfulConnection _contentfulConnection = contentfulConnection;

    private readonly ContentLocales _contentLocales = contentLocales;
    private readonly ContentType _sourceContentType1 = sourceContentType1;
    private readonly ContentType _sourceContentType2 = sourceContentType2;
    private readonly ContentType? _sourceContentType3 = sourceContentType3;
    private readonly ContentType _targetContentType = targetContentType;
    private readonly string? _source2EntryId = source2EntryId;

    public override async Task<int> GetRecordCountAsync()
    {
        if (_results != null)
        {
            _currentRecordIndex = 0;
            return _results.Count;
        }

        List<Field> targetFieldsList = [];

        var targetField1 = _targetContentType.Fields
            .Where(f => f.Validations.Any(v => v is LinkContentTypeValidator vLink && vLink.ContentTypeIds.Contains(_cuteContentJoin.SourceContentType1)))
            .FirstOrDefault()
            ?? throw new CliException($"No reference field for content type '{_cuteContentJoin.SourceContentType1}' found in '{_cuteContentJoin.TargetContentType}'");

        var targetField2 = _targetContentType.Fields
            .Where(f => f.Validations.Any(v => v is LinkContentTypeValidator vLink && vLink.ContentTypeIds.Contains(_cuteContentJoin.SourceContentType2)))
            .FirstOrDefault()
            ?? throw new CliException($"No reference field for content type '{_cuteContentJoin.SourceContentType2}' found in '{_cuteContentJoin.TargetContentType}'");

        targetFieldsList.AddRange(targetField1, targetField2);

        List<List<Entry<JObject>>> entriesList = [];

        var entries1 =
            _contentfulConnection.GetManagementEntries<Entry<JObject>>(
                new EntryQuery.Builder()
                    .WithContentType(_sourceContentType1)
                    .WithQueryString(_cuteContentJoin.SourceQueryString1 ?? string.Empty)
                    .Build()
            )
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            //.Where(e => source1AllKeys || source1Keys.Contains(e.Fields["key"]?.Value<string>()))
            .ToList();

        var entries2 = _contentfulConnection.GetManagementEntries<Entry<JObject>>(
                new EntryQuery.Builder()
                    .WithContentType(_sourceContentType2.SystemProperties.Id)
                    .WithQueryString(_cuteContentJoin.SourceQueryString2 ?? string.Empty)
                    .Build()
            )
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            //.Where(e => source2AllKeys || source2Keys.Contains(e.Fields["key"]?.Value<string>()))
            .ToList();

        entriesList.AddRange(entries1, entries2);

        if (_sourceContentType3 != null)
        {
            var targetField3 = _targetContentType.Fields
                .Where(f => f.Validations.Any(v => v is LinkContentTypeValidator vLink && vLink.ContentTypeIds.Contains(_cuteContentJoin.SourceContentType3)))
                .FirstOrDefault()
                ?? throw new CliException($"No reference field for content type '{_cuteContentJoin.SourceContentType3}' found in '{_cuteContentJoin.TargetContentType}'");

            var entries3 = _contentfulConnection.GetManagementEntries<Entry<JObject>>(
                    new EntryQuery.Builder()
                        .WithContentType(_sourceContentType3.SystemProperties.Id)
                        .WithQueryString(_cuteContentJoin.SourceQueryString3 ?? string.Empty)
                        .Build()
                )
                .ToBlockingEnumerable()
                .Select(e => e.Entry)
                //.Where(e => source2AllKeys || source2Keys.Contains(e.Fields["key"]?.Value<string>()))
                .ToList();

            targetFieldsList.Add(targetField3);
            entriesList.Add(entries3);
        }

        await Task.Delay(0);

        var defaultLocale = _contentLocales.DefaultLocale;

        var targetSerializer = new EntrySerializer(_targetContentType, new ContentLocales([], defaultLocale));

        _results = [];

        var totalCount = entriesList[0].Count;

        for (int i = 1; i < entriesList.Count; i++)
        {
            totalCount *= entriesList[i].Count;
        }

        void recursiveJoin(int depth, List<Entry<JObject>> currentEntries)
        {
            if (depth == entriesList.Count)
            {
                var joinKey = string.Join(".", currentEntries.Select(e => e.Fields.SelectToken($"key.{defaultLocale}")?.Value<string>()));
                var joinTitle = string.Join(" | ", currentEntries.Select(e => e.Fields.SelectToken($"title.{defaultLocale}")?.Value<string>()));//$"{entry1.Fields.SelectToken($"title.{defaultLocale}")?.Value<string>()} | {entry2.Fields.SelectToken($"title.{defaultLocale}")?.Value<string>()}";
                var joinName = currentEntries.Last().Fields.SelectToken($"name.{defaultLocale}")?.Value<string>();

                var newFlatEntry = targetSerializer.CreateNewFlatEntry();
                newFlatEntry[$"key.{defaultLocale}"] = joinKey;
                newFlatEntry[$"title.{defaultLocale}"] = joinTitle;
                newFlatEntry[$"name.{defaultLocale}"] = joinName;

                for (int i = 0; i < currentEntries.Count; i++)
                {
                    var fieldKey = $"{targetFieldsList[i].Id}.{defaultLocale}";
                    var sysId = currentEntries[i].SystemProperties.Id;
                    newFlatEntry[fieldKey] = sysId;
                }

                _results.Add(newFlatEntry);
                CountProgressNotifier?.Invoke(_results.Count, totalCount, $"Generating joined entry {_results.Count}/{totalCount}");
            }
            else
            {
                foreach (var entry in entriesList[depth])
                {
                    currentEntries.Add(entry);
                    recursiveJoin(depth + 1, currentEntries);
                    currentEntries.RemoveAt(currentEntries.Count - 1);
                }
            }
        }

        recursiveJoin(0, []);

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