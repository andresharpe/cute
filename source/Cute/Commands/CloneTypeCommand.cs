using Contentful.Core.Models;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

public sealed class CloneTypeCommand : LoggedInCommand<CloneTypeCommand.Settings>
{
    private readonly ILogger<TypeGenCommand> _logger;
    private readonly HttpClient _httpClient;
    private readonly BulkActionExecutor _bulkActionExecutor;

    public CloneTypeCommand(IConsoleWriter console, ILogger<TypeGenCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings,
        HttpClient httpClient, BulkActionExecutor bulkActionExecutor)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _logger = logger;

        _httpClient = httpClient;
        _bulkActionExecutor = bulkActionExecutor;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--content-type")]
        [Description("Specifies the content type to generate types for. Default is all.")]
        public string? ContentType { get; set; } = null!;

        [CommandOption("-o|--output")]
        [Description("The local path to output the generated types to")]
        public string OutputPath { get; set; } = default!;

        [CommandOption("-l|--language")]
        [Description("The language to generate types for (TypeScript/CSharp)")]
        public GenTypeLanguage Language { get; set; } = GenTypeLanguage.TypeScript!;

        [CommandOption("-n|--namespace")]
        [Description("The optional namespace for the generated type")]
        public string? Namespace { get; set; } = default!;

        [CommandOption("-e|--environment")]
        [Description("The optional namespace for the generated type")]
        public string? Environment { get; set; } = default!;

        [CommandOption("-p|--publish")]
        [Description("Whether to publish the created content or not. Useful if njo circular references exist.")]
        public bool Publish { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (settings.OutputPath is null)
        {
            settings.OutputPath = Directory.GetCurrentDirectory();
        }
        else if (settings.OutputPath is not null)
        {
            if (Directory.Exists(settings.OutputPath))
            {
                var dir = new DirectoryInfo(settings.OutputPath);
                settings.OutputPath = dir.FullName;
            }
            else
            {
                throw new CliException($"Path {Path.GetFullPath(settings.OutputPath)} does not exist.");
            }
        }

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

        var envOptions = new OptionsForEnvironmentProvider(_appSettings, settings.Environment!);

        var envClient = new ContentfulConnection(_httpClient, envOptions);

        _console.WriteNormalWithHighlights($"Comparing content types between environments: {ContentfulEnvironmentId} <--> {settings.Environment}", Globals.StyleHeading);

        ContentType? contentTypesMain = null;
        ContentType? contentTypesEnv = null;

        try
        {
            contentTypesEnv = await envClient.ManagementClient.GetContentType(contentTypeId);
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
            await CreateContentType(contentTypeId, contentTypesEnv);

            _console.WriteNormalWithHighlights($"Success. Created {contentTypeId} in {ContentfulEnvironmentId}", Globals.StyleHeading);
        }
        else
        {
            await _bulkActionExecutor
                .WithContentType(contentTypeId)
                .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                .Execute(BulkAction.Delete);

            await ContentfulManagementClient.DeactivateContentType(contentTypeId);

            await ContentfulManagementClient.DeleteContentType(contentTypeId);

            _console.WriteNormalWithHighlights($"Deleted {contentTypeId} in {ContentfulEnvironmentId}", Globals.StyleHeading);

            await CreateContentType(contentTypeId, contentTypesEnv);

            _console.WriteNormalWithHighlights($"Success. Created {contentTypeId} in {ContentfulEnvironmentId}", Globals.StyleHeading);
        }

        var createEntries = ContentfulEntryEnumerator.Entries<Entry<JObject>>(envClient.ManagementClient, contentTypeId)
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .ToList();

        await _bulkActionExecutor
            .WithContentType(contentTypeId)
            .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
            .WithNewEntries(createEntries)
            .WithConcurrentTaskLimit(25)
            .WithPublishChunkSize(100)
            .WithMillisecondsBetweenCalls(120)
            .Execute(BulkAction.Upsert);

        if (settings.Publish)
        {
            await _bulkActionExecutor
                .WithContentType(contentTypeId)
                .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                .Execute(BulkAction.Publish);
        }

        _console.WriteBlankLine();
        _console.WriteAlert("Done!");

        return 0;
    }

    private async Task CreateContentType(string contentTypeId, ContentType? contentTypesEnv)
    {
        if (contentTypesEnv is null) return;

        contentTypesEnv.Name = contentTypesEnv.Name
            .RemoveEmojis()
            .Trim();

        await ContentfulManagementClient.CreateOrUpdateContentType(contentTypesEnv);

        await ContentfulManagementClient.ActivateContentType(contentTypeId, 1);
    }
}