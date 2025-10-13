using Contentful.Core.Models;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.InputAdapters.Base;
using Cute.Lib.Serializers;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace Cute.Lib.Contentful.BulkActions.Actions;

/// <summary>
/// Streaming version of UpsertBulkAction that processes large datasets in batches to avoid memory issues
/// </summary>
public class StreamingUpsertBulkAction(
    ContentfulConnection contentfulConnection, 
    HttpClient httpClient, 
    bool appendFields = false) 
    : BulkActionBase(contentfulConnection, httpClient)
{
    private readonly bool _appendFields = appendFields;
    private readonly int _batchSize = 1000; // Configurable batch size
    private readonly int _maxConcurrentContentfulQueries = 10; // Limit concurrent Contentful API calls

    public override IList<ActionProgressIndicator> ActionProgressIndicators() =>
    [
        new() { Intent = "Estimating record count..." },
        new() { Intent = "Processing data in batches..." },
        new() { Intent = "Comparing and upserting entries..." },
    ];

    public StreamingUpsertBulkAction WithBatchSize(int batchSize)
    {
        return new StreamingUpsertBulkAction(_contentfulConnection, _httpClient, _appendFields);
    }

    /// <summary>
    /// Executes the streaming upsert operation in memory-efficient batches
    /// </summary>
    public override async Task<IEnumerable<string>> ExecuteAsync(Action<BulkActionProgressEvent>[]? progressUpdaters = null)
    {
        if (_withNewEntriesAdapter is not IStreamingInputAdapter streamingAdapter)
        {
            throw new CliException("Streaming upsert requires a streaming input adapter. Use regular UpsertBulkAction for non-streaming adapters.");
        }

        ValidateConfiguration();

        var processedIds = new List<string>();
        var estimatedCount = await streamingAdapter.GetEstimatedRecordCountAsync();
        
        progressUpdaters?[0]?.Invoke(new(estimatedCount, estimatedCount, 
            $"Estimated {estimatedCount} entries to process", null));

        var totalProcessed = 0;
        var serializer = new EntrySerializer(_contentType!, _contentLocales!);

        // Create a lookup cache for existing entries (with size limit to prevent memory issues)
        var existingEntriesCache = new ConcurrentDictionary<string, Entry<JObject>>();
        var cacheMaxSize = Math.Min(10000, estimatedCount / 10); // Cache up to 10k entries or 10% of dataset

        // Process data in streaming batches
        await foreach (var batch in streamingAdapter.GetRecordBatchesAsync(_batchSize))
        {
            var batchList = batch.ToList();
            progressUpdaters?[1]?.Invoke(new(totalProcessed, estimatedCount, 
                $"Processing batch of {batchList.Count} entries...", null));

            var processedBatch = await ProcessBatchAsync(batchList, serializer, existingEntriesCache, cacheMaxSize);
            processedIds.AddRange(processedBatch);

            totalProcessed += batchList.Count;
            
            progressUpdaters?[2]?.Invoke(new(totalProcessed, estimatedCount, 
                $"Processed {totalProcessed}/{estimatedCount} entries", null));

            // Optional: Force garbage collection periodically for large datasets
            if (totalProcessed % (5 * _batchSize) == 0)
            {
                GC.Collect(1, GCCollectionMode.Optimized);
            }
        }

        var finalCount = Math.Max(1, processedIds.Count);
        progressUpdaters?[2]?.Invoke(new(finalCount, finalCount, 
            $"Completed processing {processedIds.Count} entries", null));

        return processedIds;
    }

    private void ValidateConfiguration()
    {
        _ = _contentType ?? throw new CliException("You need to call 'WithContentType' before 'Execute'");
        _ = _contentLocales ?? throw new CliException("You need to call 'WithContentLocales' before 'Execute'");
        _ = _withNewEntriesAdapter ?? throw new CliException("You need to call 'WithNewEntries' before 'Execute'");
    }

    private async Task<List<string>> ProcessBatchAsync(
        List<IDictionary<string, object?>> batch, 
        EntrySerializer serializer,
        ConcurrentDictionary<string, Entry<JObject>> existingEntriesCache,
        int cacheMaxSize)
    {
        var processedIds = new List<string>();
        var entriesToUpsert = new List<Entry<JObject>>();

        // Convert flat entries to Entry objects and determine which need upserting
        foreach (var flatEntry in batch)
        {
            try
            {
                var entryId = flatEntry["sys.Id"]?.ToString();
                var matchKey = GetMatchKey(flatEntry);

                // Try to find existing entry
                Entry<JObject>? existingEntry = null;
                
                if (entryId != null && existingEntriesCache.TryGetValue(entryId, out existingEntry))
                {
                    // Found in cache
                }
                else if (matchKey != null)
                {
                    // Try to find by match key in cache
                    existingEntry = existingEntriesCache.Values
                        .FirstOrDefault(e => GetEntryMatchKey(e) == matchKey);
                    
                    if (existingEntry == null && existingEntriesCache.Count < cacheMaxSize)
                    {
                        // Query Contentful for this specific entry
                        existingEntry = await QueryContentfulForMatchKey(matchKey);
                        if (existingEntry != null)
                        {
                            existingEntriesCache.TryAdd(existingEntry.SystemProperties.Id, existingEntry);
                        }
                    }
                }

                // Create new entry from flat data
                if (entryId == null)
                {
                    flatEntry["sys.Id"] = ContentfulIdGenerator.NewId();
                }
                
                flatEntry["sys.Version"] ??= 0;
                
                var newEntry = serializer.DeserializeEntry(flatEntry);

                // Determine if upsert is needed
                if (existingEntry == null)
                {
                    // New entry
                    entriesToUpsert.Add(newEntry);
                    processedIds.Add(newEntry.SystemProperties.Id);
                }
                else if (HasChanges(flatEntry, existingEntry, serializer))
                {
                    // Entry has changes - merge with existing
                    var existingFlatEntry = serializer.SerializeEntry(existingEntry);
                    MergeEntries(flatEntry, existingFlatEntry);
                    var updatedEntry = serializer.DeserializeEntry(existingFlatEntry);
                    entriesToUpsert.Add(updatedEntry);
                    processedIds.Add(updatedEntry.SystemProperties.Id);
                }
                // If no changes, skip this entry
            }
            catch (Exception ex)
            {
                NotifyUserInterfaceOfError($"Error processing entry: {ex.Message}");
                continue;
            }
        }

        // Upsert entries in this batch
        if (_applyChanges && entriesToUpsert.Count > 0)
        {
            await UpsertEntriesConcurrently(entriesToUpsert);
        }
        else if (entriesToUpsert.Count > 0)
        {
            NotifyUserInterface($"Batch would upsert {entriesToUpsert.Count} entries. Use -a|--apply to apply changes.");
        }

        return processedIds;
    }

    private string? GetMatchKey(IDictionary<string, object?> flatEntry)
    {
        if (_matchField == null) return null;
        
        var matchKey = $"{_matchField}.{_contentLocales!.DefaultLocale}";
        return flatEntry.TryGetValue(matchKey, out var value) ? value?.ToString() : null;
    }

    private string? GetEntryMatchKey(Entry<JObject> entry)
    {
        if (_matchField == null) return null;
        
        return entry.Fields[_matchField]?[_contentLocales!.DefaultLocale]?.ToString();
    }

    private async Task<Entry<JObject>?> QueryContentfulForMatchKey(string matchKey)
    {
        try
        {
            // Query Contentful for entry with specific match key value
            var query = new EntryQuery.Builder()
                .WithContentType(_contentType!.SystemProperties.Id)
                .WithFieldEquals(_matchField!, matchKey)
                .WithLimit(1)
                .Build();

            await foreach (var (entry, _) in _contentfulConnection.GetManagementEntries<Entry<JObject>>(query))
            {
                return entry; // Return first match
            }
        }
        catch (Exception ex)
        {
            NotifyUserInterfaceOfError($"Error querying Contentful for match key '{matchKey}': {ex.Message}");
        }

        return null;
    }

    private bool HasChanges(IDictionary<string, object?> localFlatEntry, Entry<JObject> existingEntry, EntrySerializer serializer)
    {
        try
        {
            var existingFlatEntry = serializer.SerializeEntry(existingEntry);
            
            foreach (var (fieldName, value) in localFlatEntry)
            {
                if (fieldName.StartsWith("sys.")) continue;
                
                if (!existingFlatEntry.TryGetValue(fieldName, out var existingValue))
                {
                    if (value != null) return true;
                    continue;
                }
                
                if (!AreValuesEqual(value, existingValue))
                {
                    return true;
                }
            }
            
            return false;
        }
        catch
        {
            // If comparison fails, assume changes exist to be safe
            return true;
        }
    }

    private static bool AreValuesEqual(object? value1, object? value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;
        
        var str1 = value1.ToString()?.Trim();
        var str2 = value2.ToString()?.Trim();
        
        return string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase);
    }

    private void MergeEntries(IDictionary<string, object?> sourceFlat, IDictionary<string, object?> targetFlat)
    {
        foreach (var (key, value) in sourceFlat)
        {
            if (key.StartsWith("sys.")) continue;
            
            if (_appendFields && targetFlat.ContainsKey(key))
            {
                // Append logic for fields that should be appended rather than replaced
                var existingValue = targetFlat[key]?.ToString() ?? "";
                var newValue = value?.ToString() ?? "";
                
                if (!string.IsNullOrEmpty(newValue) && !existingValue.Contains(newValue))
                {
                    targetFlat[key] = string.IsNullOrEmpty(existingValue) ? newValue : $"{existingValue}, {newValue}";
                }
            }
            else
            {
                targetFlat[key] = value;
            }
        }
    }

    private async Task UpsertEntriesConcurrently(List<Entry<JObject>> entries)
    {
        var semaphore = new SemaphoreSlim(_maxConcurrentContentfulQueries);
        var tasks = entries.Select(async entry =>
        {
            await semaphore.WaitAsync();
            try
            {
                var displayField = _contentType?.DisplayField;
                var displayValue = displayField != null 
                    ? entry.Fields[displayField]?[_contentLocales!.DefaultLocale]?.Value<string>() ?? ""
                    : "";

                await _contentfulConnection.CreateOrUpdateEntryAsync(
                    entry.Fields,
                    entry.SystemProperties.Id,
                    entry.SystemProperties.Version ?? 0,
                    _contentTypeId,
                    $"Upserting '{_contentTypeId}' '{displayValue.Snip(40)}'",
                    (m) => NotifyUserInterface(m),
                    (e) => NotifyUserInterfaceOfError(e)
                );
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}