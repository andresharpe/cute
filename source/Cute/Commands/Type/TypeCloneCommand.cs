using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Type;

public class TypeCloneCommand(IConsoleWriter console, ILogger<TypeCloneCommand> logger, AppSettings appSettings, HttpClient httpClient)
    : BaseLoggedInCommand<TypeCloneCommand.Settings>(console, logger, appSettings)
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
        var contentTypeId = settings.ContentTypeId;

        var contentfulEnvironment = await ContentfulConnection.GetDefaultEnvironmentAsync();

        if (settings.SourceEnvironmentId == contentfulEnvironment.Id())
        {
            throw new CliException("You can not clone a content type in the same environment because content id's will clash.");
        }

        var sourceEnvOptions = new OptionsForEnvironmentProvider(AppSettings, settings.SourceEnvironmentId!);

        var sourceEnvClient = new ContentfulConnection.Builder()
            .WithHttpClient(_httpClient)
            .WithOptionsProvider(sourceEnvOptions)
            .Build();

        _console.WriteNormalWithHighlights($"Comparing content types between environments: {contentfulEnvironment.Id()} <--> {settings.SourceEnvironmentId}", Globals.StyleHeading);

        ContentType? targetContentType = null;
        ContentType? sourceContentType = null;

        try
        {
            sourceContentType = await sourceEnvClient.GetContentTypeAsync(contentTypeId);
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights($"{contentTypeId} found in environment {settings.SourceEnvironmentId}", Globals.StyleHeading);
        }
        catch (Exception ex)
        {
            throw new CliException(ex.Message);
        }

        try
        {
            targetContentType = await GetContentTypeOrThrowError(contentTypeId);
        }
        catch { }

        if (!ConfirmWithPromptChallenge($"clone {contentTypeId} from {settings.SourceEnvironmentId} to {contentfulEnvironment.Id()}"))
        {
            return -1;
        }

        if (targetContentType is null)
        {
            _console.WriteNormalWithHighlights($"The content type {contentTypeId} does not exist in {contentfulEnvironment.Id()}", Globals.StyleHeading);
            _console.WriteBlankLine();

            targetContentType = await ContentfulConnection.CloneContentTypeAsync(sourceContentType, contentTypeId);

            _console.WriteNormalWithHighlights($"Success. Created {contentTypeId} in {contentfulEnvironment.Id()}", Globals.StyleHeading);
        }
        else
        {
            throw new CliException($"Content type {contentTypeId} already exists in {contentfulEnvironment.Id()}. Please manually delete existing type by running a 'type delete' command");
        }

        _console.WriteNormalWithHighlights($"Reading entries {contentTypeId} in {settings.SourceEnvironmentId}", Globals.StyleHeading);

        var createEntries = sourceEnvClient
            .GetManagementEntries<Entry<JObject>>(contentTypeId)
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .ToList();

        _console.WriteNormalWithHighlights($"{createEntries.Count} entries found...", Globals.StyleHeading);

        var bulkActions = new List<IBulkAction> {
            new UpsertBulkAction(ContentfulConnection, _httpClient)
                .WithContentType(sourceContentType)
                .WithContentLocales(await ContentfulConnection.GetContentLocalesAsync())
                .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                .WithNewEntries(createEntries)
                .WithConcurrentTaskLimit(settings.EntriesPerBatch)
                .WithPublishChunkSize(100)
                .WithMillisecondsBetweenCalls(120)
        };

        bulkActions.Add(
            new PublishBulkAction(ContentfulConnection, _httpClient)
            .WithContentType(sourceContentType)
            .WithContentLocales(await ContentfulConnection.GetContentLocalesAsync())
            .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
            .WithConcurrentTaskLimit(settings.EntriesPerBatch)
            .WithApplyChanges(settings.Publish)
        );

        await PerformBulkOperations(bulkActions.ToArray());

        _console.WriteBlankLine();
        _console.WriteAlert("Done!");

        return 0;
    }
}