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
        [Description("The id of the Contentful join entry to generate content for.")]
        public string JoinId { get; set; } = default!;

        [CommandOption("-i|--entry-id")]
        [Description("Id of source 2 entry to join content for.")]
        public string? Source2EntryId { get; set; } = null;
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
        var joinEntry = CuteContentJoin.GetByKey(ContentfulConnection, settings.JoinId);

        if (joinEntry == null)
        {
            throw new CliException($"No join definition with key '{settings.JoinId}' found.");
        }

        // Load contentId's

        var source1ContentType = await GetContentTypeOrThrowError(joinEntry.SourceContentType1);
        var source2ContentType = await GetContentTypeOrThrowError(joinEntry.SourceContentType2);
        var targetContentType = await GetContentTypeOrThrowError(joinEntry.TargetContentType);

        // Load Entries
        await PerformBulkOperations(
            [
                new UpsertBulkAction(ContentfulConnection, _httpClient)
                    .WithContentType(targetContentType)
                    .WithContentLocales(await ContentfulConnection.GetContentLocalesAsync())
                    .WithMatchField("key")
                    .WithNewEntries(
                        new JoinEntriesAdapter(
                            joinEntry,
                            ContentfulConnection,
                            await ContentfulConnection.GetContentLocalesAsync(),
                            source1ContentType,
                            source2ContentType,
                            targetContentType,
                            settings.Source2EntryId
                        )
                    )
                    .WithVerbosity(settings.Verbosity),

                new PublishBulkAction(ContentfulConnection, _httpClient)
                    .WithContentType(targetContentType)
                    .WithContentLocales(await ContentfulConnection.GetContentLocalesAsync())
                    .WithVerbosity(settings.Verbosity)
            ]
        );

        return 0;
    }
}