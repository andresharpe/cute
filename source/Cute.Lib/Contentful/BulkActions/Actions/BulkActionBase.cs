using Contentful.Core.Models;
using Cute.Lib.Contentful.BulkActions.Models;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.InputAdapters;
using Cute.Lib.InputAdapters.EntryAdapters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;

namespace Cute.Lib.Contentful.BulkActions.Actions;

public abstract class BulkActionBase(ContentfulConnection contentfulConnection, HttpClient httpClient) : IBulkAction
{
    protected int _publishChunkSize = 100;

    protected int _bulkActionCallLimit = 5;

    protected int _millisecondsBetweenCalls = 100;

    protected int _concurrentTaskLimit = 100;

    protected ContentfulConnection _contentfulConnection = contentfulConnection;

    protected readonly HttpClient _httpClient = httpClient;

    protected string? _contentTypeId;

    protected ContentType? _contentType;

    protected Action<FormattableString>? _displayAction;

    protected Verbosity _verbosity = Verbosity.Normal;

    protected List<BulkItem>? _withEntries;

    protected IInputAdapter? _withNewEntriesAdapter;

    protected List<Entry<JObject>>? _withUpdatedFlatEntries;

    protected List<IDictionary<string, object?>>? _withFlatEntries;

    protected Dictionary<string, Entry<JObject>>? _forComparisonEntries;

    protected ContentLocales? _contentLocales;

    protected string? _matchField;

    protected bool _applyChanges = false;

    protected bool _useContext = false;

    protected IEnumerable<string> _contextIds { get; set; } = default!;

    public abstract IList<ActionProgressIndicator> ActionProgressIndicators();

    public abstract Task<IEnumerable<string>> ExecuteAsync(Action<BulkActionProgressEvent>[]? progressUpdaters = null);

    public BulkActionBase WithContentfulConnection(ContentfulConnection contentfulConnection)
    {
        _contentfulConnection = contentfulConnection;
        return this;
    }

    [Obsolete("This method is deprecated. Use WithContentType() instead.")]
    public BulkActionBase WithContentTypeId(string contentTypeId)
    {
        _contentTypeId = contentTypeId;
        return this;
    }

    public BulkActionBase WithContentType(ContentType contentType)
    {
        _contentType = contentType;
        _contentTypeId = contentType.SystemProperties.Id;
        return this;
    }

    public BulkActionBase WithContentLocales(ContentLocales contentLocales)
    {
        _contentLocales = contentLocales;
        return this;
    }

    public BulkActionBase WithPublishChunkSize(int publishChunkSize)
    {
        _publishChunkSize = publishChunkSize;
        return this;
    }

    public BulkActionBase WithBulkActionCallLimit(int bulkActionCallLimit)
    {
        _bulkActionCallLimit = bulkActionCallLimit;
        return this;
    }

    public BulkActionBase WithMillisecondsBetweenCalls(int millisecondsBetweenCalls)
    {
        _millisecondsBetweenCalls = millisecondsBetweenCalls;
        return this;
    }

    public BulkActionBase WithConcurrentTaskLimit(int concurrentTaskLimit)
    {
        _concurrentTaskLimit = concurrentTaskLimit;
        return this;
    }

    public BulkActionBase WithDisplayAction(Action<FormattableString> displayAction)
    {
        _displayAction = displayAction;
        return this;
    }

    public BulkActionBase WithVerbosity(Verbosity verbosity)
    {
        _verbosity = verbosity;
        return this;
    }

    public BulkActionBase WithEntries(List<BulkItem> withEntries)
    {
        _withEntries = withEntries;
        return this;
    }

    public BulkActionBase WithMatchField(string? matchField)
    {
        _matchField = matchField;
        return this;
    }

    public BulkActionBase WithApplyChanges(bool applyChanges)
    {
        _applyChanges = applyChanges;
        return this;
    }

    public BulkActionBase WithUseContext(bool useContext)
    {
        _useContext = useContext;
        return this;
    }

    public BulkActionBase WithContextIds(IEnumerable<string> contextIds)
    {
        _contextIds = contextIds;
        return this;
    }

    public BulkActionBase WithNewEntries(List<Entry<JObject>> withNewEntries, string? sourceName = null)
    {
        _ = _contentType ?? throw new CliException("'WithContentType' and 'WithContentLocales' must be called before 'WithNewEntries'");

        _ = _contentLocales ?? throw new CliException("'WithContentType' and 'WithContentLocales' must be called before 'WithNewEntries'");

        _withNewEntriesAdapter = new EntryListInputAdapter(withNewEntries, _contentType, _contentLocales, sourceName);

        _withUpdatedFlatEntries = withNewEntries;

        return this;
    }

    public BulkActionBase WithNewEntries(List<IDictionary<string, object?>> withNewEntries, string? sourceName = null)
    {
        _ = _contentType ?? throw new CliException("'WithContentType' and 'WithContentLocales' must be called before 'WithNewEntries'");

        _ = _contentLocales ?? throw new CliException("'WithContentType' and 'WithContentLocales' must be called before 'WithNewEntries'");

        _withNewEntriesAdapter = new FlatEntryListInputAdapter(withNewEntries, sourceName);

        _withUpdatedFlatEntries = null;

        return this;
    }

    public BulkActionBase WithNewEntries(IInputAdapter inputAdapter)
    {
        _withNewEntriesAdapter = inputAdapter;

        _withUpdatedFlatEntries = null;

        return this;
    }

    protected async Task<List<BulkItem>> GetAllEntries(Action<BulkActionProgressEvent>? progressUpdater)
    {
        var allItems = new List<BulkItem>();

        _ = _contentType ?? throw new CliException("You need to call 'WithContentType' before 'Execute'");
        _ = _contentLocales ?? throw new CliException("You need to call 'WithContentLocales' before 'Execute'");

        var displayField = _contentType.DisplayField;

        var contentTypeId = _contentType.SystemProperties.Id;

        var i = 0;

        await foreach (var (entry, total) in
            _contentfulConnection.GetManagementEntries<Entry<JObject>>(_contentType))
        {
            var displayFieldValue = entry.Fields[displayField]?[_contentLocales.DefaultLocale]?.Value<string>() ?? string.Empty;

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

            progressUpdater?.Invoke(new(i, total, null, null));

            if (++i % 1000 == 0)
            {
                NotifyUserInterface($"Getting '{_contentTypeId}' entry {i}/{total}...", progressUpdater);
            }
        }

        return allItems;
    }

    protected async Task ChangePublishRequiredEntries(List<BulkItem> items, BulkAction bulkAction,
                    Action<BulkActionProgressEvent>? progressUpdater)
    {
        var bulkActionIds = new Dictionary<string, (string Status, BulkActionResponse Response, BulkItem[] Items)>();

        var processed = 0;

        var totalSucceded = 0;

        var totalCount = items.Count;

        var chunkQueue = new ConcurrentQueue<BulkItem[]>();

        foreach (var chunk in items.Chunk(_publishChunkSize))
        {
            chunkQueue.Enqueue(chunk);
        }

        while (!chunkQueue.IsEmpty || bulkActionIds.Count != 0)
        {
            processed += _publishChunkSize;

            if (!chunkQueue.TryDequeue(out var chunk)) continue;

            var bulkRequest = await BuildBulkRequest(chunk, bulkAction, progressUpdater);

            if (bulkRequest is null) continue;

            var (BulkRequestId, Response, Items) = bulkRequest.Value;

            bulkActionIds.Add(BulkRequestId, new(Response.Sys.Status, Response, Items));

            if (bulkActionIds.Count < _bulkActionCallLimit && !chunkQueue.IsEmpty)
            {
                continue;
            }

            while (bulkActionIds.Count > 0)
            {
                var delay = 0;

                foreach (var bulkActionId in bulkActionIds.ToArray())
                {
                    delay += _millisecondsBetweenCalls;

                    var bulkActionStatus = await SendBulkActionStatusRequestViaHttp(bulkActionId.Key, progressUpdater);

                    var status = bulkActionStatus.Sys.Status;

                    if (bulkActionId.Value.Status != status)
                    {
                        totalSucceded += status == "succeeded" ? bulkActionId.Value.Items.Length : 0;

                        NotifyUserInterface($"...checking action '{bulkActionId.Key}' and it's status is '{status}' (Succeeded={totalSucceded}/{totalCount})");

                        bulkActionIds[bulkActionId.Key] = new(status, bulkActionId.Value.Response, bulkActionId.Value.Items);
                    }

                    if (status == "succeeded")
                    {
                        bulkActionIds.Remove(bulkActionId.Key);

                        NotifyUserInterface($"Bulk action '{bulkActionId.Key}' (Succeeded={totalSucceded}/{totalCount})", progressUpdater);
                    }
                    else if (status == "failed")
                    {
                        NotifyUserInterfaceOfError($"Bulk action '{bulkActionId.Key}' failed. Reason:'{bulkActionStatus.Error?.Sys?.Id}'", progressUpdater);

                        bulkActionIds.Remove(bulkActionId.Key);

                        var retry1Count = bulkActionId.Value.Items.Length / 2;

                        chunkQueue.Enqueue(bulkActionId.Value.Items.Take(retry1Count).ToArray());

                        chunkQueue.Enqueue(bulkActionId.Value.Items.Skip(retry1Count).ToArray());
                    }
                }

                progressUpdater?.Invoke(new(totalSucceded, totalCount, null, null));

                if (bulkActionIds.Count < _bulkActionCallLimit && !chunkQueue.IsEmpty)
                {
                    break;
                }
            }
        }

        progressUpdater?.Invoke(new(totalSucceded, totalCount, null, null));
    }

    private async Task<(string BulkRequestId, BulkActionResponse Response, BulkItem[] Items)?> BuildBulkRequest(BulkItem[] chunk,
        BulkAction bulkAction, Action<BulkActionProgressEvent>? progressUpdater)
    {
        var bulkActionResponse = await SendBulkChangePublishRequestViaHttp(bulkAction, chunk, progressUpdater);

        if (bulkActionResponse is null) return null;

        var bulkActionResponseId = bulkActionResponse.Sys.Id;

        var bulkActionResponseStatus = bulkActionResponse.Sys.Status;

        var entriesCount = chunk.Length;

        var bulkActionName = bulkAction.ToString().ToUpper();

        NotifyUserInterface($"Created bulk {bulkActionName} of '{_contentTypeId}' action '{bulkActionResponseId}' with status '{bulkActionResponseStatus}' ({entriesCount} entries)");

        return new(bulkActionResponseId, bulkActionResponse, chunk);
    }

    private async Task<BulkActionResponse> SendBulkChangePublishRequestViaHttp(BulkAction bulkAction, IEnumerable<BulkItem> items, Action<BulkActionProgressEvent>? progressUpdater)
    {
        var spaceId = (await _contentfulConnection.GetDefaultSpaceAsync()).SystemProperties.Id;
        var environmentId = (await _contentfulConnection.GetDefaultEnvironmentAsync()).SystemProperties.Id;

        var bulkEndpoint = new Uri($"https://api.contentful.com/spaces/{spaceId}/environments/{environmentId}/bulk_actions/{bulkAction.ToString().ToLower()}");

        object bulkObject = bulkAction == BulkAction.Publish
            ? items.Select(i => new { sys = new { id = i.Sys.Id, type = "Link", linkType = "Entry", version = i.Sys.Version ?? 1 } }).ToArray()
            : items.Select(i => new { sys = new { id = i.Sys.Id, type = "Link", linkType = "Entry" } }).ToArray();

        var bulkRequest = new HttpRequestMessage
        {
            RequestUri = bulkEndpoint,
            Method = HttpMethod.Post,
            Content = new StringContent(JsonConvert.SerializeObject(new { entities = new { items = bulkObject } }), Encoding.UTF8, "application/json")
        };

        bulkRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _contentfulConnection.ManagementApiKey);

        using var bulkResponse = await ContentfulConnection.RateLimiter.SendRequestAsync(
                () => _httpClient.SendAsync(bulkRequest),
                $"Sending bulk {bulkAction} request...",
                (m) => NotifyUserInterface(m),
                (m) => NotifyUserInterfaceOfError(m, progressUpdater)
            );

        bulkResponse.EnsureSuccessStatusCode();

        var responseText = await bulkResponse.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<BulkActionResponse>(responseText) ??
            throw new CliException("Could not read the bulk action response.");
    }

    private async Task<BulkActionResponse> SendBulkActionStatusRequestViaHttp(string bulkActionResponseId, Action<BulkActionProgressEvent>? progressUpdater)
    {
        var spaceId = (await _contentfulConnection.GetDefaultSpaceAsync()).SystemProperties.Id;
        var environmentId = (await _contentfulConnection.GetDefaultEnvironmentAsync()).SystemProperties.Id;

        var bulkEndpoint = new Uri($"https://api.contentful.com/spaces/{spaceId}/environments/{environmentId}/bulk_actions/actions/{bulkActionResponseId}");

        var bulkRequest = new HttpRequestMessage
        {
            RequestUri = bulkEndpoint,
            Method = HttpMethod.Get,
        };

        bulkRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _contentfulConnection.ManagementApiKey);

        using var bulkResponse = await ContentfulConnection.RateLimiter.SendRequestAsync(
                () => _httpClient.SendAsync(bulkRequest),
                    $"",
                    (m) => { }, // suppress this message
                    (e) => NotifyUserInterfaceOfError(e, progressUpdater)
            );

        bulkResponse.EnsureSuccessStatusCode();

        var responseText = await bulkResponse.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<BulkActionResponse>(responseText)
            ?? throw new CliException("Could not read the bulk action response.");
    }

    protected void NotifyUserInterface(FormattableString message, Action<BulkActionProgressEvent>? progressUpdater = null)
    {
        if (_verbosity >= Verbosity.Detailed)
        {
            _displayAction?.Invoke(message);
        }
        progressUpdater?.Invoke(new(null, null, message, null));
    }

    protected void NotifyUserInterfaceOfError(FormattableString message, Action<BulkActionProgressEvent>? progressUpdater = null)
    {
        if (_verbosity >= Verbosity.Detailed)
        {
            _displayAction?.Invoke(message);
        }
        progressUpdater?.Invoke(new(null, null, null, message));
    }

    protected async Task PublishWithEntries(Action<BulkActionProgressEvent>? progressUpdater)
    {
        _ = _withEntries ?? throw new InvalidOperationException("Entries must be loaded before publishing.");

        var entries = _withEntries.Where(entry => !entry.Sys.IsPublished() || entry.Sys.IsChanged()).ToList();

        var count = Math.Max(1, entries.Count);

        if (_useContext)
        {
            entries = entries.Where(item => _contextIds.Contains(item.Sys.Id)).ToList();
        }

        progressUpdater?.Invoke(new(0, count, $"Publishing {entries.Count} entries...", null));

        await ChangePublishRequiredEntries(entries, BulkAction.Publish, progressUpdater);

        count = Math.Max(count, 1);

        progressUpdater?.Invoke(new(count, count, $"Found and published {entries.Count} entries.", null));
    }

    protected async Task UnPublishWithEntries(Action<BulkActionProgressEvent>? progressUpdater)
    {
        _ = _withEntries ?? throw new InvalidOperationException("Entries must be loaded before publishing.");

        var entries = _withEntries.Where(entry => entry.Sys.PublishedAt is not null).ToList();

        var count = Math.Max(1, entries.Count);

        progressUpdater?.Invoke(new(0, count, $"Unpublishing {entries.Count} entries...", null));

        await ChangePublishRequiredEntries(entries, BulkAction.Unpublish, progressUpdater);

        count = Math.Max(count, 1);

        progressUpdater?.Invoke(new(count, count, $"Found and unpublished {entries.Count} entries.", null));
    }

    protected async Task GetWithEntries(Action<BulkActionProgressEvent>? progressUpdater)
    {
        progressUpdater?.Invoke(new(0, 1, $"Reading Contentful entries for '{_contentTypeId}'.", null));

        _withEntries ??= await GetAllEntries(progressUpdater);

        var count = Math.Max(_withEntries.Count, 1);

        progressUpdater?.Invoke(new(count, count, $"Read {_withEntries.Count} '{_contentTypeId}' to find unpublished entries.", null));
    }
}

public class ActionProgressIndicator
{
    public string Intent { get; init; } = "Working...";
}