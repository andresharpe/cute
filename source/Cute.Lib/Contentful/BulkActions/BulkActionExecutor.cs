using Contentful.Core.Errors;
using Contentful.Core.Models;
using Cute.Lib.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;

namespace Cute.Lib.Contentful.BulkActions;

public class BulkActionExecutor
{
    private int _publishChunkSize = 200;

    private int _bulkActionCallLimit = 5;

    private int _millisecondsBetweenCalls = 100;

    private int _concurrentTaskLimit = 100;

    private readonly ContentfulConnection _contentfulConnection;

    private readonly HttpClient _httpClient;

    private string? _contentType;

    private ContentType? _contentTypeDefinition;

    private Action<FormattableString>? _displayAction;

    private List<BulkItem>? _withEntries;

    private List<Entry<JObject>>? _withNewEntries;

    public BulkActionExecutor(ContentfulConnection contentfulConnection, HttpClient httpClient)
    {
        _contentfulConnection = contentfulConnection;
        _httpClient = httpClient;
    }

    public BulkActionExecutor WithContentType(string contentType)
    {
        _contentType = contentType;
        return this;
    }

    public BulkActionExecutor WithPublishChunkSize(int publishChunkSize)
    {
        _publishChunkSize = publishChunkSize;
        return this;
    }

    public BulkActionExecutor WithBulkActionCallLimit(int bulkActionCallLimit)
    {
        _bulkActionCallLimit = bulkActionCallLimit;
        return this;
    }

    public BulkActionExecutor WithMillisecondsBetweenCalls(int millisecondsBetweenCalls)
    {
        _millisecondsBetweenCalls = millisecondsBetweenCalls;
        return this;
    }

    public BulkActionExecutor WithConcurrentTaskLimit(int concurrentTaskLimit)
    {
        _concurrentTaskLimit = concurrentTaskLimit;
        return this;
    }

    public BulkActionExecutor WithDisplayAction(Action<FormattableString> displayAction)
    {
        _displayAction = displayAction;
        return this;
    }

    public BulkActionExecutor WithEntries(List<BulkItem> withEntries)
    {
        _withEntries = withEntries;
        return this;
    }

    public BulkActionExecutor WithNewEntries(List<Entry<JObject>> withNewEntries)
    {
        _withNewEntries = withNewEntries;
        return this;
    }

    private async Task ChangePublishRequiredEntries(List<BulkItem> items, BulkAction bulkAction)
    {
        var bulkActionIds = new Dictionary<string, (string Status, BulkActionResponse Response, BulkItem[] Items)>();

        var totalSucceded = 0;

        var totalItems = items.Count;

        var chunkQueue = new ConcurrentQueue<BulkItem[]>();

        foreach (var chunk in items.Chunk(_publishChunkSize))
        {
            chunkQueue.Enqueue(chunk);
        }

        var whileDelay = _millisecondsBetweenCalls;

        while (!chunkQueue.IsEmpty || bulkActionIds.Count != 0)
        {
            whileDelay += _millisecondsBetweenCalls;

            if (!chunkQueue.TryDequeue(out var chunk)) continue;

            var (BulkRequestId, Response, Items) = await BuildBulkRequest(chunk, bulkAction, whileDelay);

            bulkActionIds.Add(BulkRequestId, new(Response.Sys.Status, Response, Items));

            if (bulkActionIds.Count < _bulkActionCallLimit && !chunkQueue.IsEmpty)
            {
                whileDelay += _millisecondsBetweenCalls;
                continue;
            }

            whileDelay = 0;

            while (bulkActionIds.Count > 0)
            {
                var delay = 0;

                foreach (var bulkActionId in bulkActionIds.ToArray())
                {
                    delay += _millisecondsBetweenCalls;

                    var bulkActionStatus = await SendBulkActionStatusRequest(bulkActionId.Key, delay);

                    var status = bulkActionStatus.Sys.Status;

                    if (bulkActionId.Value.Status != status)
                    {
                        totalSucceded += status == "succeeded" ? bulkActionId.Value.Items.Length : 0;

                        _displayAction?.Invoke($"...checking action '{bulkActionId.Key}' and it's status is '{status}' (Succeeded={totalSucceded}/{totalItems})");

                        bulkActionIds[bulkActionId.Key] = new(status, bulkActionId.Value.Response, bulkActionId.Value.Items);
                    }

                    if (status == "succeeded")
                    {
                        bulkActionIds.Remove(bulkActionId.Key);
                    }
                    else if (status == "failed")
                    {
                        _displayAction?.Invoke($"...action '{bulkActionId.Key}' failed. Reason '{bulkActionStatus.Error?.Sys?.Id}' (Succeeded={totalSucceded}/{totalItems})");

                        bulkActionIds.Remove(bulkActionId.Key);

                        var retry1Count = bulkActionId.Value.Items.Length / 2;

                        chunkQueue.Enqueue(bulkActionId.Value.Items.Take(retry1Count).ToArray());

                        chunkQueue.Enqueue(bulkActionId.Value.Items.Skip(retry1Count).ToArray());
                    }
                }

                if (bulkActionIds.Count < _bulkActionCallLimit && !chunkQueue.IsEmpty)
                {
                    break;
                }
            }
        }
    }

    private async Task<(string BulkRequestId, BulkActionResponse Response, BulkItem[] Items)> BuildBulkRequest(BulkItem[] chunk, BulkAction bulkAction, int delay)
    {
        var bulkActionResponse = await SendBulkChangePublishRequest(bulkAction, chunk, delay);

        var bulkActionResponseId = bulkActionResponse.Sys.Id;

        var bulkActionResponseStatus = bulkActionResponse.Sys.Status;

        var entriesCount = chunk.Length;

        var bulkActionName = bulkAction.ToString().ToUpper();

        _displayAction?.Invoke($"Created bulk {bulkActionName} of '{_contentType}' action '{bulkActionResponseId}' with status '{bulkActionResponseStatus}' ({entriesCount} entries)");

        return new(bulkActionResponseId, bulkActionResponse, chunk);
    }

    private async Task<BulkActionResponse> SendBulkActionStatusRequest(string bulkActionResponseId, int delay)
    {
        await Task.Delay(delay);

        var bulkEndpoint = new Uri($"https://api.contentful.com/spaces/{_contentfulConnection.Options.SpaceId}/environments/{_contentfulConnection.Options.Environment}/bulk_actions/actions/{bulkActionResponseId}");

        var bulkRequest = new HttpRequestMessage
        {
            RequestUri = bulkEndpoint,
            Method = HttpMethod.Get,
        };

        bulkRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _contentfulConnection.Options.ManagementApiKey);

        using var bulkResponse = await _httpClient.SendAsync(bulkRequest);

        bulkResponse.EnsureSuccessStatusCode();

        var responseText = await bulkResponse.Content.ReadAsStringAsync();

        var bulkActionResponseCheck = JsonConvert.DeserializeObject<BulkActionResponse>(responseText) ??
        throw new CliException("Could not read the bulk action response.");

        return bulkActionResponseCheck;
    }

    private async Task<BulkActionResponse> SendBulkChangePublishRequest(BulkAction bulkAction, IEnumerable<BulkItem> items, int delay)
    {
        await Task.Delay(delay);

        var bulkEndpoint = new Uri($"https://api.contentful.com/spaces/{_contentfulConnection.Options.SpaceId}/environments/{_contentfulConnection.Options.Environment}/bulk_actions/{bulkAction.ToString().ToLower()}");

        object bulkObject = bulkAction == BulkAction.Publish
            ? items.Select(i => new { sys = new { id = i.Sys.Id, type = "Link", linkType = "Entry", version = i.Sys.Version ?? 1 } }).ToArray()
            : items.Select(i => new { sys = new { id = i.Sys.Id, type = "Link", linkType = "Entry" } }).ToArray();

        var bulkRequest = new HttpRequestMessage
        {
            RequestUri = bulkEndpoint,
            Method = HttpMethod.Post,
            Content = new StringContent(JsonConvert.SerializeObject(new { entities = new { items = bulkObject } }), Encoding.UTF8, "application/json")
        };

        bulkRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _contentfulConnection.Options.ManagementApiKey);

        using var bulkResponse = await _httpClient.SendAsync(bulkRequest);

        // bulkResponse.EnsureSuccessStatusCode();

        var responseText = await bulkResponse.Content.ReadAsStringAsync();

        var bulkActionResponse = JsonConvert.DeserializeObject<BulkActionResponse>(responseText) ??
        throw new CliException("Could not read the bulk action response.");

        return bulkActionResponse;
    }

    private async Task<List<BulkItem>> GetAllEntries(string contentType, int delay)
    {
        await Task.Delay(delay);

        var allItems = new List<BulkItem>();

        var displayField = _contentTypeDefinition?.DisplayField;

        var i = 0;

        await foreach (var (entry, _) in ContentfulEntryEnumerator.Entries<Entry<JObject>>(_contentfulConnection.ManagementClient, contentType, displayField))
        {
            var displayFieldValue = displayField == null
                ? string.Empty
                : entry.Fields[displayField]?["en"]?.Value<string>() ?? string.Empty;

            allItems.Add(new BulkItem()
            {
                Sys = new Sys()
                {
                    Id = entry.SystemProperties.Id,
                    PublishedAt = entry.SystemProperties.PublishedAt,
                    PublishedVersion = entry.SystemProperties.PublishedVersion,
                    ArchivedVersion = entry.SystemProperties.ArchivedVersion,
                    Version = entry.SystemProperties.Version,
                    DisplayFieldValue = displayFieldValue
                }
            });

            if (++i % 1000 == 0)
            {
                _displayAction?.Invoke($"Getting '{_contentType}' entry {i}...");
            }
        }

        return allItems;
    }

    private async Task DeleteRequiredEntries(List<BulkItem> allEntries)
    {
        var tasks = new Task[_concurrentTaskLimit];
        var taskNo = 0;
        var count = allEntries.Count;
        var processed = 0;
        var delay = _millisecondsBetweenCalls;

        await Task.Delay(_millisecondsBetweenCalls * 10);

        foreach (var item in allEntries)
        {
            var itemId = item.Sys.Id;
            var itemVersion = item.Sys.Version ?? 0;
            var displayFieldValue = item.Sys.DisplayFieldValue;

            processed++;
            delay += _millisecondsBetweenCalls;

            _displayAction?.Invoke($"...deleting '{_contentType}' item '{itemId}' ({processed}/{count}) {displayFieldValue}");

            tasks[taskNo++] = DeleteEntry(itemId, itemVersion, delay);

            if (taskNo >= tasks.Length)
            {
                Task.WaitAll(tasks);
                taskNo = 0;
                delay = _millisecondsBetweenCalls;
            }
        }

        Task.WaitAll(tasks.Where(t => t is not null).ToArray());
    }

    private async Task DeleteEntry(string itemId, int itemVersion, int delayToStart)
    {
        var retryAttempt = 0;
        var isSuccess = false;

        while (!isSuccess)
        {
            try
            {
                await Task.Delay(delayToStart);

                await _contentfulConnection.ManagementClient.DeleteEntry(itemId, itemVersion);

                isSuccess = true;
            }
            catch (ContentfulRateLimitException)
            {
                retryAttempt++;
                _displayAction?.Invoke($"...waiting. Contentful rate limit exceeded. Retry attempt {retryAttempt}");
            }
        }
    }

    private async Task UpsertRequiredEntries(List<Entry<JObject>> entries)
    {
        if (entries.Count == 0) return;

        var tasks = new Task[_concurrentTaskLimit];
        var taskNo = 0;

        var count = entries.Count;
        var processed = 0;
        var displayField = _contentTypeDefinition?.DisplayField;
        var delay = 0;

        await Task.Delay(_millisecondsBetweenCalls * 10);

        foreach (var newEntry in entries)
        {
            delay += _millisecondsBetweenCalls;

            var itemId = newEntry.SystemProperties.Id;
            var itemVersion = newEntry.SystemProperties.Version ?? 0;
            var displayFieldValue = displayField == null
                ? string.Empty
                : newEntry.Fields[displayField]?["en"]?.Value<string>() ?? string.Empty;

            processed++;

            _displayAction?.Invoke($"...creating/updating '{_contentType}' item '{itemId}' ({processed}/{count}) {displayFieldValue}");

            tasks[taskNo++] = CreateOrUpdateEntry(
                delay,
                newEntry.Fields,
                id: newEntry.SystemProperties.Id,
                version: newEntry.SystemProperties.Version ?? 0,
                contentTypeId: _contentType);

            if (taskNo >= tasks.Length)
            {
                Task.WaitAll(tasks);
                taskNo = 0;
                delay = 0;
            }
        }

        Task.WaitAll(tasks.Where(t => t is not null).ToArray());
    }

    private async Task CreateOrUpdateEntry(int delay, JObject fields, string id, int version, string? contentTypeId)
    {
        await Task.Delay(delay);

        await _contentfulConnection.ManagementClient.CreateOrUpdateEntry(
                fields,
                id: id,
                version: version,
                contentTypeId: contentTypeId
            );
    }

    public async Task Execute(BulkAction bulkAction)
    {
        if (_contentType is null) throw new CliException("No content type specified");

        _contentTypeDefinition = await _contentfulConnection.ManagementClient.GetContentType(_contentType);

        var allEntries = _withEntries ?? await GetAllEntries(_contentType, _millisecondsBetweenCalls);

        if (bulkAction == BulkAction.Publish)
        {
            var entries = allEntries.Where(entry => !entry.Sys.IsPublished() || entry.Sys.IsChanged()).ToList();
            var count = entries.Count;
            _displayAction?.Invoke($"{count} results to publish...");
            await ChangePublishRequiredEntries(entries, BulkAction.Publish);
        }

        if (bulkAction == BulkAction.Upsert)
        {
            if (_withNewEntries is null) throw new CliException("No entries that requires create or update specified");
            var entries = allEntries.ToList();
            var count = entries.Count;
            _displayAction?.Invoke($"{count} results to create/update...");
            await UpsertRequiredEntries(_withNewEntries);
        }

        if (bulkAction == BulkAction.Unpublish || bulkAction == BulkAction.Delete)
        {
            var entries = allEntries.Where(entry => entry.Sys.PublishedAt is not null).ToList();
            var count = entries.Count;
            _displayAction?.Invoke($"{count} results to unpublish...");
            await ChangePublishRequiredEntries(entries, BulkAction.Unpublish);
        }

        if (bulkAction == BulkAction.Delete)
        {
            var count = allEntries.Count;
            _displayAction?.Invoke($"{count} results to delete...");
            await DeleteRequiredEntries(allEntries);
        }
    }
}