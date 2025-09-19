using Contentful.Core.Models;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.Serializers;
using Newtonsoft.Json.Linq;

namespace Cute.Lib.Contentful.BulkActions.Actions;

public class UpsertBulkAction(ContentfulConnection contentfulConnection, HttpClient httpClient)
    : BulkActionBase(contentfulConnection, httpClient)
{

    private readonly bool _appendFields = false;

    public UpsertBulkAction(ContentfulConnection contentfulConnection, HttpClient httpClient, bool appendFields)
        : this(contentfulConnection, httpClient)
    {
        _appendFields = appendFields;
    }

    public override IList<ActionProgressIndicator> ActionProgressIndicators() =>
    [
        new() { Intent = "Counting new entries..." },
        new() { Intent = "Getting new entries..." },
        new() { Intent = "Getting Contentful entries..." },
        new() { Intent = "Comparing entries..." },
        new() { Intent = "Upserting entries..." },
    ];

    public override async Task<IEnumerable<string>> ExecuteAsync(Action<BulkActionProgressEvent>[]? progressUpdaters = null)
    {
        await GetNewAdapterEntries(progressUpdaters?[0], progressUpdaters?[1]);

        await GetAllEntriesForComparison(progressUpdaters?[2]);

        await CompareAndMergeEntries(progressUpdaters?[3]);

        if (_applyChanges)
        {
            await UpsertRequiredEntries(_withUpdatedFlatEntries!, progressUpdaters?[4]);
        }
        else
        {
            NotifyUserInterface($"No changes applied. Use -a|--apply to apply changes.", progressUpdaters?[4]);
        }

        return _withUpdatedFlatEntries!.Select(e => e.SystemProperties.Id);
    }

    private async Task GetNewAdapterEntries(Action<BulkActionProgressEvent>? progressUpdaterCount, Action<BulkActionProgressEvent>? progressUpdaterRead)
    {
        int count = 0;

        if (_withNewEntriesAdapter is null)
            throw new CliException("You need to specify entries to upsert with 'WithNewEntries' method before 'Execute'.");

        await GetAllEntriesFromAdapter(progressUpdaterCount, progressUpdaterRead);

        MergeNewAdapterEntries();

        //TODO: Revisit this and either make optional or remove.
        //CleanUpLocalizations(_withFlatEntries!);

        count = Math.Max(1, _withFlatEntries?.Count ?? throw new CliException($"Unexpected null value."));

        progressUpdaterCount?.Invoke(new(count, count, $"Counted {_withFlatEntries.Count} '{_contentTypeId}' entries from '{_withNewEntriesAdapter.SourceName.Snip(60)}'.", null));
        progressUpdaterRead?.Invoke(new(count, count, $"Read {_withFlatEntries.Count} '{_contentTypeId}' entries from '{_withNewEntriesAdapter.SourceName.Snip(60)}'.", null));
    }

    private async Task GetAllEntriesFromAdapter(Action<BulkActionProgressEvent>? progressUpdaterCount, Action<BulkActionProgressEvent>? progressUpdaterRead)
    {
        var newEntries = new List<Entry<JObject>>();

        _ = _contentType ?? throw new CliException("You need to call 'WithContentType' before 'Execute'");

        _ = _contentLocales ?? throw new CliException("You need to call 'WithContentLocales' before 'Execute'");

        _ = _withNewEntriesAdapter ?? throw new CliException("You need to call 'WithNewEntries' before 'Execute'");

        var displayField = _contentType.DisplayField;

        var contentTypeId = _contentType.SystemProperties.Id;

        var displayKey = $"{displayField}.{_contentLocales.DefaultLocale}";

        _withNewEntriesAdapter.ActionNotifier = (m) => NotifyUserInterface(m, progressUpdaterRead);

        _withNewEntriesAdapter.ErrorNotifier = (m) => NotifyUserInterfaceOfError(m, progressUpdaterRead);

        _withNewEntriesAdapter.CountProgressNotifier = (i, total, m) => progressUpdaterCount?.Invoke(new(i, total, m, null));

        var totalRecords = await _withNewEntriesAdapter.GetRecordCountAsync();

        var i = 0;

        _withFlatEntries = [];

        await foreach (var flatEntry in _withNewEntriesAdapter.GetRecordsAsync())
        {
            _withFlatEntries.Add(flatEntry);

            progressUpdaterRead?.Invoke(new(i, totalRecords, null, null));

            if (++i % 1000 == 0)
            {
                NotifyUserInterface($"Getting '{_contentTypeId}' entry {i}/{totalRecords}...", progressUpdaterRead);
            }
        }
    }

    private void MergeNewAdapterEntries()
    {
        if (_matchField is not null)
        {
            _ = _contentLocales ?? throw new CliException("You need to call 'WithContentLocales' before 'Execute'");

            _matchField = _matchField.RemoveFromEnd($".{_contentLocales.DefaultLocale}");

            var matchKey = $"{_matchField}.{_contentLocales.DefaultLocale}";

            var duplicateKeyEntries = _withFlatEntries!
                .GroupBy(e => e[matchKey]?.ToString())
                .Where(e => e.Count() >= 2)
                .ToDictionary(e => e.Key ?? string.Empty, e => e.ToList());

            _withFlatEntries =_withFlatEntries!
                .GroupBy(e => e[matchKey]?.ToString())
                .Where(e => e.Count() == 1)
                .Select(g => g.First())
                .ToList();

            foreach (var (key, entries) in duplicateKeyEntries)
            {
                HashSet<string> conflictingFields = new HashSet<string>();
                var mergedEntry = entries.First();
                for (var i = 1; i < entries.Count; i++)
                {
                    conflictingFields.UnionWith(mergedEntry.MergeWith(entries[i]));
                }

                conflictingFields.ExceptWith(EntrySerializer.SysFields);

                if (conflictingFields.Count > 0)
                {
                    NotifyUserInterfaceOfError($"Merge conflict detected for entry with the key: '{key}', conflicting fields: '{string.Join(", ", conflictingFields)}'");
                }

                _withFlatEntries.Add(mergedEntry);
            }
        }
    }

    private void CleanUpLocalizations(List<IDictionary<string, object?>> flatEntries)
    {
        foreach (var flatEntry in flatEntries)
        {
            CleanupFlatEntryLocalizations(flatEntry);
        }
    }

    private void CleanupFlatEntryLocalizations(IDictionary<string, object?> flatEntry)
    {
        Dictionary<string, object?> defaultLocaleFields = new();
        foreach (var (key, value) in flatEntry)
        {
            if (key.EndsWith($".{_contentLocales!.DefaultLocale}"))
            {
                defaultLocaleFields[key.RemoveFromEnd($".{_contentLocales!.DefaultLocale}")] = value;
            }
        }

        foreach (var (key, value) in flatEntry)
        {
            if (!key.Contains(".") || key.EndsWith($".{_contentLocales!.DefaultLocale}"))
            {
                continue;
            }

            if (value is null)
            {
                flatEntry.Remove(key);
                continue;
            }

            var keyBase = key.Substring(0, key.LastIndexOf('.'));
            if (defaultLocaleFields.TryGetValue(keyBase, out var defaultValue))
            {
                if (string.Equals(value?.ToString(), defaultValue?.ToString()))
                {
                    flatEntry.Remove(key);
                }
            }
        }
    }

    private async Task GetAllEntriesForComparison(Action<BulkActionProgressEvent>? progressUpdater)
    {
        _ = _contentType ?? throw new CliException("You need to call 'WithContentType' before 'Execute'");

        _ = _contentTypeId ?? throw new CliException("You need to call 'WithContentType' before 'Execute'");

        _ = _contentLocales ?? throw new CliException("You need to call 'WithContentLocales' before 'Execute'");

        _ = _withFlatEntries ?? throw new CliException("You need to call 'WithNewEntries' before 'Execute'");

        if (_forComparisonEntries is not null && _forComparisonEntries.Count > 0)
        {
            throw new CliException("You should not call 'WithEntries' before 'Execute'");
        }

        var steps = -1;

        var currentStep = 1;

        var flatEntries = _withFlatEntries ?? [];

        var localIdIndex = flatEntries
                .Select(e => e["sys.Id"]?.ToString())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToHashSet();

        if (_matchField is not null)
        {
            _matchField = _matchField.RemoveFromEnd($".{_contentLocales.DefaultLocale}");
        }

        var matchKey = $"{_matchField}.{_contentLocales.DefaultLocale}";

        if (_matchField is not null)
        {
            var duplicateKeys = flatEntries
                .GroupBy(e => e[matchKey]?.ToString())
                .Where(e => e.Count() >= 2)
                .Select(e => e.Key)
                .ToHashSet();

            if (duplicateKeys.Count > 0)
            {
                NotifyUserInterfaceOfError($"{duplicateKeys.Count} duplicate keys found in input data!", progressUpdater);

                NotifyUserInterfaceOfError($"Duplicate keys: '{string.Join(", ", duplicateKeys)}'");

                flatEntries = flatEntries
                    .GroupBy(e => e[matchKey]?.ToString())
                    .Select(g => g.First())
                    .ToList();

                _withFlatEntries = flatEntries;
            }
        }

        var localKeyIndex = _matchField is null ? [] :
            flatEntries
                .Select(e => e[matchKey]?.ToString())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToHashSet();

        var contentDisplayField = _contentType.DisplayField;

        _forComparisonEntries = [];

        await foreach (var (entry, total) in
            _contentfulConnection.GetManagementEntries<Entry<JObject>>(_contentType))
        {
            if (steps == -1)
            {
                steps = total;
            }

            progressUpdater?.Invoke(new(currentStep++, steps, null, null));

            var id = entry.SystemProperties.Id;

            if (localIdIndex.Contains(id))
            {
                _forComparisonEntries.Add(id, entry);
                continue;
            }

            if (_matchField is null) continue;

            var key = entry.Fields[_matchField]?[_contentLocales.DefaultLocale]?.ToString();

            if (key is null) continue;

            if (localKeyIndex.Contains(key))
            {
                _forComparisonEntries.TryAdd(key, entry);
                continue;
            }
        }

        var count = Math.Max(1, _forComparisonEntries.Count);

        progressUpdater?.Invoke(new(count, count, $"Retrieved {_forComparisonEntries.Count} existing '{_contentTypeId}' entries from Contentful space.", null));
    }

    public async Task CompareAndMergeEntries(Action<BulkActionProgressEvent>? progressUpdater)
    {
        _ = _contentType ?? throw new CliException("You need to call 'WithContentType' before 'Execute'");

        _ = _contentTypeId ?? throw new CliException("You need to call 'WithContentType' before 'Execute'");

        _ = _contentLocales ?? throw new CliException("You need to call 'WithContentLocales' before 'Execute'");

        _ = _withFlatEntries ?? throw new CliException("You need to call 'WithNewEntries' before 'Execute'");

        _ = _forComparisonEntries ?? throw new CliException("You need to call 'WithNewEntries' before 'Execute'");

        _ = _withNewEntriesAdapter ?? throw new CliException("You need to call 'WithNewEntries' before 'Execute'");

        await Task.Delay(0);

        var serializer = new EntrySerializer(_contentType, _contentLocales);

        var matchedEntries = 0;
        var newLocalEntries = 0;
        var mismatchedValues = 0;

        var steps = _withFlatEntries.Count;

        var currentStep = 1;

        var keyField = $"{_matchField}.{_contentLocales.DefaultLocale}";

        var contentDisplayField = _contentType.DisplayField;

        _withUpdatedFlatEntries = [];

        var fields = serializer.ColumnFieldNames;

        foreach (var localFlatEntry in _withFlatEntries)
        {
            var missingFields = localFlatEntry.Where(kv => fields.Contains(kv.Key) == false).Select(kv => kv.Key).ToArray();
            if (missingFields.Length > 0)
            {
                throw new CliException($"Local entry contains fields '{string.Join(", ", missingFields)}' that are not in content type '{_contentTypeId}'.");
            }

            Entry<JObject>? cloudEntry = null;

            var id = localFlatEntry["sys.Id"]?.ToString();

            if (id is null || !_forComparisonEntries.TryGetValue(id, out cloudEntry))
            {
                if (_matchField is not null)
                {
                    var keyValue = localFlatEntry[keyField]?.ToString();
                    if (keyValue is not null)
                    {
                        _forComparisonEntries.TryGetValue(keyValue, out cloudEntry);
                    }
                }
            }

            if (cloudEntry is null)
            {
                localFlatEntry["sys.Id"] ??= ContentfulIdGenerator.NewId();

                if (!localFlatEntry.TryGetValue("sys.Version", out var version))
                {
                    localFlatEntry["sys.Version"] = null;
                }
                localFlatEntry["sys.Version"] ??= 0;

                var newEntry = serializer.DeserializeEntry(localFlatEntry);

                _withUpdatedFlatEntries.Add(newEntry);

                newLocalEntries++;
            }
            else
            {
                var newEntry = serializer.DeserializeEntry(localFlatEntry);
                var cloudFlatEntry = serializer.SerializeEntry(cloudEntry);

                //TODO Revisit this and either make optional or remove.
                //CleanupFlatEntryLocalizations(cloudFlatEntry);

                if (ValuesDiffer(localFlatEntry, cloudFlatEntry, serializer, progressUpdater)
                    || ArrayElementCountsDiffer(cloudEntry.Fields, newEntry.Fields)) // this is sometimes necessary as the cloud may contain a single string in an array field that looks like an array (contains commas), but isn't
                {
                    EnsureFallbacks(cloudFlatEntry);
                    _withUpdatedFlatEntries.Add(serializer.DeserializeEntry(cloudFlatEntry));

                    mismatchedValues++;
                }
                else
                {
                    matchedEntries++;
                }
            }

            progressUpdater?.Invoke(new(currentStep++, steps, null, null));
        }

        var count = Math.Max(1, _withUpdatedFlatEntries.Count);

        progressUpdater?.Invoke(new(count, count, $"Compared local entries with Contentful: {newLocalEntries} new, {mismatchedValues} changed, {matchedEntries} matched.", null));
    }

    // Remove empty and null values from the flat entry to ensure contentful localization fallbacks are applied correctly.
    private void EnsureFallbacks(IDictionary<string, object?> flatEntry)
    {
        foreach (var (key, value) in flatEntry)
        {
            if (!key.Contains(".") || key.EndsWith($".{_contentLocales!.DefaultLocale}"))
            {
                continue;
            }

            if (string.IsNullOrEmpty(value?.ToString()))
            {
                flatEntry.Remove(key);
            }
        }
    }

    private bool ValuesDiffer(
        IDictionary<string, object?> localFlatEntry, IDictionary<string, object?> cloudFlatEntry,
        EntrySerializer serializer, Action<BulkActionProgressEvent>? progressUpdater)
    {
        var isChanged = false;

        Dictionary<string, (string?, object?)> changedFields = [];

        var defaultLocale = _contentLocales!.DefaultLocale;

        var contentDisplayField = _contentType!.DisplayField;

        foreach (var (fieldName, value) in localFlatEntry)
        {
            if (fieldName.StartsWith("sys.")) continue;

            string? oldValue = null;

            if (cloudFlatEntry.TryGetValue(fieldName, out var oldValueObj))
            {
                oldValue = cloudFlatEntry[fieldName]?.ToString();
            }

            var isFieldChanged = serializer.CompareAndUpdateEntry(cloudFlatEntry, fieldName, value, _appendFields);

            if (isFieldChanged)
            {
                changedFields.Add(fieldName, (oldValue, value));
            }

            isChanged = isFieldChanged || isChanged;
        }

        if (isChanged)
        {
            var newEntryName = localFlatEntry[$"{contentDisplayField}.{defaultLocale}"];

            NotifyUserInterface($"'{_contentTypeId}' - '{newEntryName}' will be updated.", progressUpdater);

            foreach (var (fieldname, value) in changedFields)
            {
                var fieldnameDisplay = fieldname;
                var valueBefore = value.Item1?.Snip(30);
                var valueAfter = value.Item2?.ToString()?.Snip(30);

                NotifyUserInterface($"...field '{fieldnameDisplay}' changed from '{valueBefore}' to '{valueAfter}'", progressUpdater);
            }
        }

        return isChanged;
    }

    public static bool ArrayElementCountsDiffer(JObject cloudEntry, JObject newEntry)
    {
        if (cloudEntry == null || newEntry == null)
            return true;

        foreach (var cloudProperty in cloudEntry.Properties())
        {
            var newProperty = newEntry.Property(cloudProperty.Name);
            if (newProperty == null)
                continue;

            if (cloudProperty.Value.Type == JTokenType.Array && newProperty.Value.Type == JTokenType.Array)
            {
                JArray cloudArray = (JArray)cloudProperty.Value;
                JArray newArray = (JArray)newProperty.Value;

                if (cloudArray.Count != newArray.Count)
                    return true;
            }
            else if (cloudProperty.Value.Type == JTokenType.Object && newProperty.Value.Type == JTokenType.Object)
            {
                // Recursively compare nested objects
                if (ArrayElementCountsDiffer((JObject)cloudProperty.Value, (JObject)newProperty.Value))
                    return true;
            }
        }

        return false;
    }

    protected async Task UpsertRequiredEntries(List<Entry<JObject>> entries,
        Action<BulkActionProgressEvent>? progressUpdater)
    {
        if (_contentTypeId == null)
        {
            _displayAction?.Invoke($"Error: The content type needs to be specified!");
            return;
        }

        if (entries.Count == 0) return;

        var tasks = new Task[_concurrentTaskLimit];
        var taskNo = 0;

        var totalCount = entries.Count;
        var processed = 0;
        var displayField = _contentType?.DisplayField;

        await Task.Delay(1);

        foreach (var newEntry in entries)
        {
            var itemId = newEntry.SystemProperties.Id;
            var itemVersion = newEntry.SystemProperties.Version ?? 0;
            var displayFieldValue = displayField == null
                ? string.Empty
                : newEntry.Fields[displayField]?[_contentLocales!.DefaultLocale]?.Value<string>() ?? string.Empty;

            processed++;

            var messageProcessed = processed;

            FormattableString message = $"Upserting '{_contentTypeId}' ({messageProcessed}/{totalCount}) '{displayFieldValue.Snip(40)}'";

            tasks[taskNo++] = _contentfulConnection.CreateOrUpdateEntryAsync(
                newEntry.Fields,
                newEntry.SystemProperties.Id,
                newEntry.SystemProperties.Version ?? 0,
                _contentTypeId,
                message,
                (m) => NotifyUserInterface(m, progressUpdater),
                (e) => NotifyUserInterfaceOfError(e, progressUpdater)
            );

            if (taskNo >= tasks.Length)
            {
                Task.WaitAll(tasks);
                taskNo = 0;
            }

            progressUpdater?.Invoke(new(processed, totalCount, null, null));
        }

        Task.WaitAll(tasks.Where(t => t is not null).ToArray());

        progressUpdater?.Invoke(new(totalCount, Math.Max(1, totalCount), $"Created or updated {totalCount} entries.", null));
    }
}