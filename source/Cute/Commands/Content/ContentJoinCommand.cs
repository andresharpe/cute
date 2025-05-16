using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Contentful.CommandModels.ContentJoinCommand;
using Cute.Lib.Exceptions;
using Cute.Lib.InputAdapters.EntryAdapters;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Content;

public class ContentJoinCommand(IConsoleWriter console, ILogger<ContentJoinCommand> logger,
    AppSettings appSettings, HttpClient httpClient)
    : BaseLoggedInCommand<ContentJoinCommand.Settings>(console, logger, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-k|--key")]
        [Description("The key of the 'cuteContentJoin' entry.")]
        public string JoinId { get; set; } = default!;

        [CommandOption("-i|--entry-id")]
        [Description("Id of source 2 entry to join content for.")]
        public string? Source2EntryId { get; set; } = null;

        [CommandOption("--no-publish")]
        [Description("Specifies whether to skip publish for modified entries")]
        public bool NoPublish { get; set; } = false;

        [CommandOption("-a|--apply")]
        [Description("Apply and publish all the required edits.")]
        public bool Apply { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.JoinId))
        {
            return ValidationResult.Error($"No content join identifier (--key) specified.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var joinEntry = ContentfulConnection.GetPreviewEntryByKey<CuteContentJoin>(settings.JoinId);

        if (joinEntry == null)
        {
            throw new CliException($"No join definition with key '{settings.JoinId}' found.");
        }

        // Load contentId's

        var source1ContentType = await GetContentTypeOrThrowError(joinEntry.SourceContentType1);
        var source2ContentType = await GetContentTypeOrThrowError(joinEntry.SourceContentType2);
        var targetContentType = await GetContentTypeOrThrowError(joinEntry.TargetContentType);

        if (!ConfirmWithPromptChallenge($"{"JOIN"} {joinEntry.SourceContentType1} and {joinEntry.SourceContentType2} entries for '{joinEntry.TargetContentType}'"))
        {
            return -1;
        }

        // Load Entries
        await PerformBulkOperations([
            new UpsertBulkAction(ContentfulConnection, _httpClient)
                    .WithContentType(targetContentType)
                    .WithContentLocales(await ContentfulConnection.GetContentLocalesAsync())
                    .WithMatchField("key")
                    .WithNewEntries(new JoinEntriesAdapter(
                            joinEntry,
                            ContentfulConnection,
                            await ContentfulConnection.GetContentLocalesAsync(),
                            source1ContentType,
                            source2ContentType,
                            targetContentType,
                            settings.Source2EntryId
                        ))
                    .WithVerbosity(settings.Verbosity)
                    .WithApplyChanges(settings.Apply),
            new PublishBulkAction(ContentfulConnection, _httpClient)
                    .WithContentType(targetContentType)
                    .WithContentLocales(await ContentfulConnection.GetContentLocalesAsync())
                    .WithVerbosity(settings.Verbosity)
                    .WithApplyChanges(!settings.NoPublish)
        ]);

        return 0;
    }
}