using Contentful.Core.Models;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;

namespace Cute.Commands;

public sealed class BulkCommand : LoggedInCommand<BulkCommand.Settings>
{
    private const int _changePublishChunkSize = 50;

    private const int _bulkActionCallLimit = 4;

    private const int _millisecondsBetweenCalls = 125;

    private readonly HttpClient _httpClient;

    public BulkCommand(IConsoleWriter console, ILogger<InfoCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings, HttpClient httpClient)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _httpClient = httpClient;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--content-type")]
        [Description("Specifies the content type to purge data from")]
        public string ContentType { get; set; } = null!;

        [CommandOption("-b|--bulk-action")]
        [Description("Specifies the bulk action to perform")]
        public BulkAction BulkAction { get; set; } = BulkAction.Publish;
    }

    public enum BulkAction
    {
        Publish,
        Unpublish,
        Delete
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _ = await base.ExecuteAsync(context, settings);

        var contentType = settings.ContentType;
        var action = settings.BulkAction.ToString().ToUpper();

        int challenge = new Random().Next(10, 100);

        var continuePrompt = new TextPrompt<int>($"[{Globals.StyleAlert.Foreground}]About to {action} all '{contentType}' entries. Enter '{challenge}' to continue:[/]")
            .PromptStyle(Globals.StyleAlertAccent);

        _console.WriteRuler();
        _console.WriteBlankLine();
        _console.WriteAlertAccent("WARNING!");
        _console.WriteBlankLine();

        var response = _console.Prompt(continuePrompt);

        if (challenge != response)
        {
            _console.WriteBlankLine();
            _console.WriteAlert("The response does not match the challenge. Aborting.");
            return -1;
        }

        _console.WriteBlankLine();

        var allEntries = await GetAllEntries(contentType);

        if (settings.BulkAction == BulkAction.Publish)
        {
            var entries = allEntries.Where(entry => entry.Sys.PublishedAt is null).ToList();
            var count = entries.Count;
            _console.WriteNormalWithHighlights($"{count} results to publish...", Globals.StyleHeading);
            await ChangePublishRequiredEntries(entries, BulkAction.Publish);
        }

        if (settings.BulkAction != BulkAction.Publish)
        {
            var entries = allEntries.Where(entry => entry.Sys.PublishedAt is not null).ToList();
            var count = entries.Count;
            _console.WriteNormalWithHighlights($"{count} results to unpublish...", Globals.StyleHeading);
            await ChangePublishRequiredEntries(entries, BulkAction.Unpublish);
        }

        if (settings.BulkAction == BulkAction.Delete)
        {
            var count = allEntries.Count;
            _console.WriteNormalWithHighlights($"{count} results to delete...", Globals.StyleHeading);
            await DeleteRequiredEntries(allEntries);
        }

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"Completed {action} of all entries of '{contentType}'.", Globals.StyleHeading);

        return 0;
    }

    private async Task DeleteRequiredEntries(List<Item> allEntries)
    {
        var tasks = new Task[50];
        var taskNo = 0;
        var itemsDeleted = 0;
        var itemsToDelete = allEntries.Count;

        foreach (var item in allEntries)
        {
            var itemId = item.Sys.Id;
            var itemVersion = item.Sys.Version ?? 0;

            _console.WriteNormalWithHighlights($"...deleting item '{itemId}' ({++itemsDeleted}/{itemsToDelete})",
                  Globals.StyleHeading);

            tasks[taskNo++] = ContentfulManagementClient.DeleteEntry(itemId, itemVersion);

            if (taskNo >= tasks.Length)
            {
                Task.WaitAll(tasks);
                taskNo = 0;
            }

            await Task.Delay(_millisecondsBetweenCalls);
        }
    }

    private async Task ChangePublishRequiredEntries(List<Item> items, BulkAction bulkAction)
    {
        var bulkActionIds = new Dictionary<string, (string Status, int Count)>();

        var totalSucceded = 0;

        var totalItems = items.Count;

        foreach (var chunk in items.Chunk(_changePublishChunkSize))
        {
            var bulkActionResponse = await SendBulkChangePublishRequest(bulkAction, chunk);

            var bulkActionResponseId = bulkActionResponse.Sys.Id;

            var bulkActionResponseStatus = bulkActionResponse.Sys.Status;

            var entriesCount = chunk.Length;

            bulkActionIds.Add(bulkActionResponseId, new(string.Empty, entriesCount));

            _console.WriteNormalWithHighlights($"Created bulk action '{bulkActionResponseId}' with status '{bulkActionResponseStatus}' ({entriesCount} entries)",
                Globals.StyleHeading);

            if (bulkActionIds.Count < _bulkActionCallLimit && entriesCount == _changePublishChunkSize)
            {
                await Task.Delay(_millisecondsBetweenCalls);
                continue;
            }

            while (bulkActionIds.Count > 0)
            {
                foreach (var bulkActionId in bulkActionIds.ToArray())
                {
                    await Task.Delay(_millisecondsBetweenCalls * 3);

                    var bulkActionStatus = await SendBulkActionStatusRequest(bulkActionId.Key);

                    var status = bulkActionStatus.Sys.Status;

                    if (bulkActionId.Value.Status != status)
                    {
                        totalSucceded += status == "succeeded" ? bulkActionIds[bulkActionId.Key].Count : 0;

                        _console.WriteNormalWithHighlights($"...checking action '{bulkActionId.Key}' and it's status is '{status}' (Succeeded={totalSucceded}/{totalItems})",
                            Globals.StyleHeading);

                        bulkActionIds[bulkActionId.Key] = new(status, bulkActionIds[bulkActionId.Key].Count);
                    }

                    if (status == "succeeded")
                    {
                        bulkActionIds.Remove(bulkActionId.Key);
                    }
                    if (status == "failed")
                    {
                        bulkActionIds.Remove(bulkActionId.Key);
                    }
                }

                if (bulkActionIds.Count < _bulkActionCallLimit && entriesCount == _changePublishChunkSize)
                {
                    break;
                }
            }
        }
    }

    private async Task<BulkActionResponse> SendBulkActionStatusRequest(string bulkActionResponseId)
    {
        var bulkEndpoint = new Uri($"https://api.contentful.com/spaces/{ContentfulSpaceId}/environments/{ContentfulEnvironmentId}/bulk_actions/actions/{bulkActionResponseId}");

        var bulkRequest = new HttpRequestMessage
        {
            RequestUri = bulkEndpoint,
            Method = HttpMethod.Get,
        };

        bulkRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _appSettings.ContentfulManagementApiKey);

        using var bulkResponse = await _httpClient.SendAsync(bulkRequest);

        var responseText = await bulkResponse.Content.ReadAsStringAsync();

        var bulkActionResponseCheck = JsonConvert.DeserializeObject<BulkActionResponse>(responseText) ??
            throw new CliException("Could not read the bulk action response.");

        return bulkActionResponseCheck;
    }

    private async Task<BulkActionResponse> SendBulkChangePublishRequest(BulkAction bulkAction, IEnumerable<Item> items)
    {
        var bulkEndpoint = new Uri($"https://api.contentful.com/spaces/{ContentfulSpaceId}/environments/{ContentfulEnvironmentId}/bulk_actions/{bulkAction.ToString().ToLower()}");

        object bulkObject = bulkAction == BulkAction.Publish
            ? items.Select(i => new { sys = new { id = i.Sys.Id, type = "Link", linkType = "Entry", version = i.Sys.Version ?? 1 } }).ToArray()
            : items.Select(i => new { sys = new { id = i.Sys.Id, type = "Link", linkType = "Entry" } }).ToArray();

        var bulkRequest = new HttpRequestMessage
        {
            RequestUri = bulkEndpoint,
            Method = HttpMethod.Post,
            Content = new StringContent(JsonConvert.SerializeObject(new { entities = new { items = bulkObject } }), Encoding.UTF8, "application/json")
        };

        bulkRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _appSettings.ContentfulManagementApiKey);

        using var bulkResponse = await _httpClient.SendAsync(bulkRequest);

        var responseText = await bulkResponse.Content.ReadAsStringAsync();

        var bulkActionResponse = JsonConvert.DeserializeObject<BulkActionResponse>(responseText) ??
            throw new CliException("Could not read the bulk action response.");

        return bulkActionResponse;
    }

    private async Task<List<Item>> GetAllEntries(string contentType)
    {
        var graphQlEndpoint = new Uri($"https://graphql.contentful.com/content/v1/spaces/{ContentfulSpaceId}/environments/{ContentfulEnvironmentId}");

        var skip = 0;
        var limit = 1000;
        var total = 0;

        var collectionName = $"{contentType}Collection";

        var allItems = new List<Item>();

        var i = 0;
        await foreach (var (entry, _) in ContentfulEntryEnumerator.Entries<Entry<JObject>>(ContentfulManagementClient, contentType))
        {
            allItems.Add(new Item()
            {
                Sys = new Sys()
                {
                    Id = entry.SystemProperties.Id,
                    PublishedAt = entry.SystemProperties.PublishedAt,
                    Version = entry.SystemProperties.Version,
                }
            });

            i++;
            if (i % 1000 == 0)
            {
                _console.WriteNormalWithHighlights($"Getting record {i}...", Globals.StyleHeading);
            }
        }

        if (i > 0) return allItems;

        while (true)
        {
            var queryObject = new
            {
                query = $$"""
                    query {
                      {{collectionName}}(preview:true limit:{{limit}} skip:{{skip}}) {
                        items {
                          sys {
                            id
                            publishedAt
                            publishedVersion
                          }
                        }
                      }
                    }
                    """
            };

            var graphQlRequest = new HttpRequestMessage
            {
                RequestUri = graphQlEndpoint,
                Method = HttpMethod.Post,
                Content = new StringContent(JsonConvert.SerializeObject(queryObject), Encoding.UTF8, "application/json")
            };

            graphQlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _appSettings.ContentfulPreviewApiKey);

            _console.WriteNormalWithHighlights($"Getting '{contentType}' entries ({total})...", Globals.StyleHeading);

            using var response = await _httpClient.SendAsync(graphQlRequest);

            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();

            var graphQlObject = JsonConvert.DeserializeObject<JObject>(responseString) ??
                throw new CliException("Could not read the entries response response.");

            var graphQlResponse = graphQlObject["data"]?[collectionName]?.ToObject<DataCollection>() ??
                throw new CliException("Could not read the entries response response.");

            total += graphQlResponse.Items.Length;

            if (graphQlResponse.Items.Length == 0) break;

            allItems.AddRange(graphQlResponse.Items);

            skip += graphQlResponse.Items.Length;
        }

        return allItems;
    }
}

public class DataCollection
{
    public Item[] Items { get; set; } = default!;
}

public class Item
{
    public Sys Sys { get; set; } = default!;
}

public class Sys
{
    public string Id { get; set; } = default!;
    public DateTime? PublishedAt { get; set; } = default!;
    public int? Version { get; set; } = default!;
}

public class BulkActionResponse
{
    public BulkActionSys Sys { get; set; } = default!;
    public string Action { get; set; } = default!;
}

public class BulkActionSys
{
    public string Type { get; set; } = default!;
    public string Id { get; set; } = default!;
    public string SchemaVersion { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = default!;
    public DateTime UpdatedAt { get; set; } = default!;
    public string Status { get; set; } = default!;
}