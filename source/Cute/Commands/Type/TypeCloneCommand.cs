using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Exceptions;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Type;

public class TypeCloneCommand(IConsoleWriter console, ILogger<TypeCloneCommand> logger, ContentfulConnection contentfulConnection,
    AppSettings appSettings, HttpClient httpClient) : BaseLoggedInCommand<TypeCloneCommand.Settings>(console, logger, contentfulConnection, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-c|--content-type-id <ID>")]
        [Description("Specifies the content type id to generate types for.")]
        public string ContentTypeId { get; set; } = null!;

        [CommandOption("--source-environment-id")]
        [Description("Specifies the source environment id.")]
        public string SourceEnvironmentId { get; set; } = default!;

        [CommandOption("-p|--publish")]
        [Description("Whether to publish the created content or not. Useful if no circular references exist.")]
        public bool Publish { get; set; } = false;

        [CommandOption("-b|--entries-per-batch")]
        [Description("Number of entries processed in parallel.")]
        public int EntriesPerBatch { get; set; } = 5;
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        string contentTypeId = settings.ContentTypeId;

        if (settings.SourceEnvironmentId == ContentfulEnvironmentId)
        {
            throw new CliException("You can not clone a content type in the same environment because content id's will clash.");
        }

        var sourceEnvOptions = new OptionsForEnvironmentProvider(_appSettings, settings.SourceEnvironmentId!);

        var sourceEnvClient = new ContentfulConnection(_httpClient, sourceEnvOptions);

        _console.WriteNormalWithHighlights($"Comparing content types between environments: {ContentfulEnvironmentId} <--> {settings.SourceEnvironmentId}", Globals.StyleHeading);

        ContentType? targetContentType = null;
        ContentType? sourceContentType = null;

        try
        {
            sourceContentType = await sourceEnvClient.ManagementClient.GetContentType(contentTypeId);
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights($"{contentTypeId} found in environment {settings.SourceEnvironmentId}", Globals.StyleHeading);
        }
        catch (Exception ex)
        {
            throw new CliException(ex.Message);
        }

        try
        {
            targetContentType = GetContentTypeOrThrowError(contentTypeId);
        }
        catch { }

        if (targetContentType is null)
        {
            _console.WriteNormalWithHighlights($"The content type {contentTypeId} does not exist in {ContentfulEnvironmentId}", Globals.StyleHeading);
            _console.WriteBlankLine();

            targetContentType = await sourceContentType.CloneWithId(ContentfulManagementClient, contentTypeId);

            _console.WriteNormalWithHighlights($"Success. Created {contentTypeId} in {ContentfulEnvironmentId}", Globals.StyleHeading);
        }
        else
        {
            throw new CliException($"Content type {contentTypeId} already exists in {ContentfulEnvironmentId}. Please manually delete existing type by running a 'type delete' command");
        }

        _console.WriteNormalWithHighlights($"Reading entries {contentTypeId} in {settings.SourceEnvironmentId}", Globals.StyleHeading);

        var createEntries = ContentfulEntryEnumerator.Entries<Entry<JObject>>(sourceEnvClient.ManagementClient, contentTypeId)
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .ToList();

        _console.WriteNormalWithHighlights($"{createEntries.Count} entries found...", Globals.StyleHeading);

        await Task.Delay(2000);

        var bulkActions = new List<IBulkAction> {
            new UpsertBulkAction(_contentfulConnection, _httpClient)
                .WithContentType(sourceContentType)
                .WithContentLocales(ContentLocales)
                .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                .WithNewEntries(createEntries)
                .WithConcurrentTaskLimit(settings.EntriesPerBatch)
                .WithPublishChunkSize(100)
                .WithMillisecondsBetweenCalls(120)
        };

        if (settings.Publish)
        {
            bulkActions.Add(
                new PublishBulkAction(_contentfulConnection, _httpClient)
                .WithContentType(sourceContentType)
                .WithContentLocales(ContentLocales)
                .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                .WithConcurrentTaskLimit(settings.EntriesPerBatch)
            );
        }

        await PerformBulkOperations(bulkActions.ToArray());

        _console.WriteBlankLine();
        _console.WriteAlert("Done!");

        return 0;
    }
}