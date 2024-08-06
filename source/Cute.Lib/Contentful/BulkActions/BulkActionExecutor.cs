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
    private int _publishChunkSize = 100;

    private int _bulkActionCallLimit = 5;

    private int _millisecondsBetweenCalls = 110;

    private int _concurrentTaskLimit = 50;

    private readonly ContentfulConnection _contentfulConnection;

    private readonly HttpClient _httpClient;

    private string? _contentType;

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

    public BulkActionExecutor WithPublishChunkSize(int publishChunkSize)
    {
        _publishChunkSize = publishChunkSize;
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

        while (!chunkQueue.IsEmpty)
        {
            if (!chunkQueue.TryDequeue(out var chunk)) continue;

            var (BulkRequestId, Response, Items) = await BuildBulkRequest(chunk, bulkAction);

            bulkActionIds.Add(BulkRequestId, new(Response.Sys.Status, Response, Items));

            if (bulkActionIds.Count < _bulkActionCallLimit && Items.Length == _publishChunkSize)
            {
                await Task.Delay(_millisecondsBetweenCalls);
                continue;
            }

            while (bulkActionIds.Count > 0)
            {
                foreach (var bulkActionId in bulkActionIds.ToArray())
                {
                    var bulkActionStatus = await SendBulkActionStatusRequest(bulkActionId.Key);

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

    private async Task<(string BulkRequestId, BulkActionResponse Response, BulkItem[] Items)> BuildBulkRequest(BulkItem[] chunk, BulkAction bulkAction)
    {
        var bulkActionResponse = await SendBulkChangePublishRequest(bulkAction, chunk);

        var bulkActionResponseId = bulkActionResponse.Sys.Id;

        var bulkActionResponseStatus = bulkActionResponse.Sys.Status;

        var entriesCount = chunk.Length;

        var bulkActionName = bulkAction.ToString().ToUpper();

        _displayAction?.Invoke($"Created bulk {bulkActionName} of '{_contentType}' action '{bulkActionResponseId}' with status '{bulkActionResponseStatus}' ({entriesCount} entries)");

        return new(bulkActionResponseId, bulkActionResponse, chunk);
    }

    private async Task<BulkActionResponse> SendBulkActionStatusRequest(string bulkActionResponseId)
    {
        await Task.Delay(_millisecondsBetweenCalls * 2);

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

    private async Task<BulkActionResponse> SendBulkChangePublishRequest(BulkAction bulkAction, IEnumerable<BulkItem> items)
    {
        await Task.Delay(_millisecondsBetweenCalls * 2);

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

        bulkResponse.EnsureSuccessStatusCode();

        var responseText = await bulkResponse.Content.ReadAsStringAsync();

        var bulkActionResponse = JsonConvert.DeserializeObject<BulkActionResponse>(responseText) ??
        throw new CliException("Could not read the bulk action response.");

        return bulkActionResponse;
    }

    private async Task<List<BulkItem>> GetAllEntries(string contentType)
    {
        await Task.Delay(_millisecondsBetweenCalls);

        var allItems = new List<BulkItem>();

        var i = 0;
        await foreach (var (entry, _) in ContentfulEntryEnumerator.Entries<Entry<JObject>>(_contentfulConnection.ManagementClient, contentType))
        {
            allItems.Add(new BulkItem()
            {
                Sys = new Sys()
                {
                    Id = entry.SystemProperties.Id,
                    PublishedAt = entry.SystemProperties.PublishedAt,
                    PublishedVersion = entry.SystemProperties.PublishedVersion,
                    ArchivedVersion = entry.SystemProperties.ArchivedVersion,
                    Version = entry.SystemProperties.Version,
                }
            });

            if (++i % 1000 == 0)
            {
                _displayAction?.Invoke($"Getting record {i}...");
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

        foreach (var item in allEntries)
        {
            await Task.Delay(_millisecondsBetweenCalls);

            var itemId = item.Sys.Id;
            var itemVersion = item.Sys.Version ?? 0;

            processed++;

            _displayAction?.Invoke($"...deleting '{_contentType}' item '{itemId}' ({processed}/{count})");

            tasks[taskNo++] = _contentfulConnection.ManagementClient.DeleteEntry(itemId, itemVersion);

            if (taskNo >= tasks.Length)
            {
                Task.WaitAll(tasks);
                taskNo = 0;
            }
        }

        Task.WaitAll(tasks.Where(t => t is not null).ToArray());
    }

    private async Task UpsertRequiredEntries(List<Entry<JObject>> entries)
    {
        if (entries.Count == 0) return;

        var tasks = new Task[_concurrentTaskLimit];
        var taskNo = 0;

        var count = entries.Count;
        var processed = 0;

        foreach (var newEntry in entries)
        {
            await Task.Delay(_millisecondsBetweenCalls);

            var itemId = newEntry.SystemProperties.Id;
            var itemVersion = newEntry.SystemProperties.Version ?? 0;

            processed++;

            _displayAction?.Invoke($"...creating/updating '{_contentType}' item '{itemId}' ({processed}/{count})");

            tasks[taskNo++] = _contentfulConnection.ManagementClient.CreateOrUpdateEntry(
                newEntry.Fields,
                id: newEntry.SystemProperties.Id,
                version: newEntry.SystemProperties.Version ?? 0,
                contentTypeId: _contentType);

            if (taskNo >= tasks.Length)
            {
                Task.WaitAll(tasks);
                taskNo = 0;
            }
        }

        Task.WaitAll(tasks.Where(t => t is not null).ToArray());
    }

    public async Task Execute(BulkAction bulkAction)
    {
        if (_contentType is null) throw new CliException("No content type specified");

        var allEntries = _withEntries ?? await GetAllEntries(_contentType);

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