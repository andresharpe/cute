using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Contentful.GraphQL;
using Cute.Lib.Exceptions;
using Cute.Services;
using Cute.UiComponents;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using static Cute.Commands.Content.ContentGenerateCommand;

namespace Cute.Commands.Content;

public class ContentGenerateCommand(IConsoleWriter console, ILogger<ContentGenerateCommand> logger,
    AppSettings appSettings,
    GenerateBulkAction generateBulkAction, HttpClient httpClient)
    : BaseLoggedInCommand<Settings>(console, logger, appSettings)
{
    private readonly GenerateBulkAction _generBulkAction = generateBulkAction;
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-k|--key")]
        [Description("The key of the 'cuteContentGenerate' entry.")]
        public string Key { get; set; } = default!;

        [CommandOption("-a|--apply")]
        [Description("Apply and publish all the required edits.")]
        public bool Apply { get; set; } = false;

        [CommandOption("-o|--operation")]
        [Description("Specify the generation operation to perform. (GenerateSingle, GenerateParallel, GenerateBatch or ListBatches)")]
        public GenerateOperation Operation { get; set; } = GenerateOperation.GenerateSingle;

        [CommandOption("--target-entry-key")]
        [Description("The key of the target entry to generate content for.")]
        public string? searchKey { get; set; } = default!;

        [CommandOption("--no-publish")]
        [Description("Specifies whether to skip publish for modified entries")]
        public bool NoPublish { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.Key))
        {
            return ValidationResult.Error("The key of the of the 'cuteContentGenerate' item is required. Specify it with '-k' or '--key'.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var defaultLocale = await ContentfulConnection.GetDefaultLocaleAsync();

        var contentMetaType = CuteContentGenerateContentType.GetContentType(defaultLocale.Code);

        var contentMetaTypeId = contentMetaType.SystemProperties.Id;

        var contentLocales = new ContentLocales([defaultLocale.Code], defaultLocale.Code);

        var apiSyncEntry = ContentfulConnection.GetPreviewEntryByKey<CuteContentGenerate>(settings.Key)
            ?? throw new CliException($"No generate entry '{contentMetaTypeId}' with key '{settings.Key}' was found.");

        var targetContentType = await GetContentTypeOrThrowError(
                GraphQLUtilities.GetContentTypeId(apiSyncEntry.CuteDataQueryEntry.Query)
            );

        if (!ConfirmWithPromptChallenge($"generate content for '{targetContentType.SystemProperties.Id}'"))
        {
            return -1;
        }

        var displayActions = new DisplayActions()
        {
            DisplayNormal = _console.WriteNormal,
            DisplayFormatted = f => _console.WriteNormalWithHighlights(f, Globals.StyleHeading),
            DisplayAlert = _console.WriteAlert,
            DisplayDim = _console.WriteDim,
            DisplayHeading = _console.WriteHeading,
            DisplayRuler = _console.WriteRuler,
            DisplayBlankLine = _console.WriteBlankLine,
        };

        _generBulkAction
            .WithContentTypes(await ContentfulConnection.GetContentTypesAsync())
            .WithGenerateOperation(settings.Operation)
            .WithContentLocales(contentLocales);

        await ProgressBars.Instance().StartAsync(async ctx =>
        {
            var taskGenerate = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.Robot}  Generating[/]");

            await _generBulkAction.GenerateContent(settings.Key,
                displayActions,
                (step, steps) =>
                {
                    taskGenerate.MaxValue = steps;
                    taskGenerate.Value = step;
                    taskGenerate.Description = $"[{Globals.StyleNormal.Foreground}]{Emoji.Known.Robot}  Generating ({step}/{steps})[/]";
                },
                searchKey: settings.searchKey
            );

            taskGenerate.StopTask();
        });

        if (settings.Operation == GenerateOperation.ListBatches)
        {
            return 0;
        }

        await PerformBulkOperations(
            [
                new PublishBulkAction(ContentfulConnection, _httpClient)
                        .WithContentType(targetContentType)
                        .WithContentLocales(await ContentfulConnection.GetContentLocalesAsync())
                        .WithVerbosity(settings.Verbosity)
                        .WithApplyChanges(!settings.NoPublish)
            ]
        );

        return 0;
    }
}