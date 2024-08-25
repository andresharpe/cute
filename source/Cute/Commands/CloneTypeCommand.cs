using Contentful.Core.Models;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Exceptions;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

public sealed class CloneTypeCommand : LoggedInCommand<CloneTypeCommand.Settings>
{
    private readonly HttpClient _httpClient;
    private readonly BulkActionExecutor _bulkActionExecutor;

    public CloneTypeCommand(IConsoleWriter console, ILogger<TypeGenCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings,
        HttpClient httpClient, BulkActionExecutor bulkActionExecutor)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _httpClient = httpClient;
        _bulkActionExecutor = bulkActionExecutor;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--content-type")]
        [Description("Specifies the content type to generate types for. Default is all.")]
        public string? ContentType { get; set; } = null!;

        [CommandOption("-e|--environment")]
        [Description("The optional namespace for the generated type")]
        public string? Environment { get; set; } = default!;

        [CommandOption("-p|--publish")]
        [Description("Whether to publish the created content or not. Useful if no circular references exist.")]
        public bool Publish { get; set; } = false;

        [CommandOption("-b|--entries-per-batch")]
        [Description("Whether to publish the created content or not. Useful if no circular references exist.")]
        public int EntriesPerBatch { get; set; } = 5;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        settings.ContentType ??= "*";

        settings.Environment ??= ContentfulEnvironmentId;

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        string contentTypeId = settings.ContentType!;

        int challenge = new Random().Next(10, 100);

        var continuePrompt = new TextPrompt<int>($"[{Globals.StyleAlert.Foreground}]About to destroy all '{contentTypeId}' entries in {ContentfulEnvironmentId}. Enter '{challenge}' to continue:[/]")
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

        if (settings.Environment == ContentfulEnvironmentId)
        {
            throw new CliException("You can not clone a content type in the same environment because content id's will clash.");
        }

        var envOptions = new OptionsForEnvironmentProvider(_appSettings, settings.Environment!);

        var envClient = new ContentfulConnection(_httpClient, envOptions);

        _console.WriteNormalWithHighlights($"Comparing content types between environments: {ContentfulEnvironmentId} <--> {settings.Environment}", Globals.StyleHeading);

        ContentType? contentTypesMain = null;
        ContentType? contentTypeEnv = null;

        try
        {
            contentTypeEnv = await envClient.ManagementClient.GetContentType(contentTypeId);
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights($"{contentTypeId} found in environment {settings.Environment}", Globals.StyleHeading);
        }
        catch (Exception ex)
        {
            throw new CliException(ex.Message);
        }

        try
        {
            contentTypesMain = await ContentfulManagementClient.GetContentType(contentTypeId);
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights($"{contentTypeId} found in environment {ContentfulEnvironmentId}", Globals.StyleHeading);
        }
        catch
        {
            _console.WriteNormalWithHighlights($"The content type {contentTypeId} does not exist in {ContentfulEnvironmentId}", Globals.StyleHeading);
        }

        _console.WriteBlankLine();

        if (contentTypesMain is null)
        {
            await contentTypeEnv.CreateWithId(ContentfulManagementClient, contentTypeId);

            _console.WriteNormalWithHighlights($"Success. Created {contentTypeId} in {ContentfulEnvironmentId}", Globals.StyleHeading);
        }
        else
        {
            await _bulkActionExecutor
                .WithContentType(contentTypeId)
                .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                .WithConcurrentTaskLimit(settings.EntriesPerBatch)
                .Execute(BulkAction.Delete);

            await ContentfulManagementClient.DeactivateContentType(contentTypeId);

            await ContentfulManagementClient.DeleteContentType(contentTypeId);

            _console.WriteNormalWithHighlights($"Deleted {contentTypeId} in {ContentfulEnvironmentId}", Globals.StyleHeading);

            await contentTypeEnv.CreateWithId(ContentfulManagementClient, contentTypeId);

            _console.WriteNormalWithHighlights($"Success. Created {contentTypeId} in {ContentfulEnvironmentId}", Globals.StyleHeading);
        }

        _console.WriteNormalWithHighlights($"Reading entries {contentTypeId} in {settings.Environment}", Globals.StyleHeading);

        var createEntries = ContentfulEntryEnumerator.Entries<Entry<JObject>>(envClient.ManagementClient, contentTypeId)
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .ToList();

        _console.WriteNormalWithHighlights($"{createEntries.Count} entries found...", Globals.StyleHeading);

        await Task.Delay(2000);

        await _bulkActionExecutor
            .WithContentType(contentTypeId)
            .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
            .WithNewEntries(createEntries)
            .WithConcurrentTaskLimit(settings.EntriesPerBatch)
            .WithPublishChunkSize(100)
            .WithMillisecondsBetweenCalls(120)
            .Execute(BulkAction.Upsert);

        if (settings.Publish)
        {
            await _bulkActionExecutor
                .WithContentType(contentTypeId)
                .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                .WithConcurrentTaskLimit(settings.EntriesPerBatch)
                .Execute(BulkAction.Publish);
        }

        _console.WriteBlankLine();
        _console.WriteAlert("Done!");

        return 0;
    }
}