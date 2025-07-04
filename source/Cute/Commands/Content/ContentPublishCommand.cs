using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Exceptions;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using static Cute.Commands.Content.ContentPublishCommand;

namespace Cute.Commands.Content;

public class ContentPublishCommand(IConsoleWriter console, ILogger<ContentPublishCommand> logger,
    AppSettings appSettings, HttpClient httpClient)
    : BaseLoggedInCommand<Settings>(console, logger, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-c|--content-type-id <ID>")]
        [Description("The Contentful content type ID.")]
        public string ContentTypeId { get; set; } = default!;

        [CommandOption("--no-publish")]
        [Description("Specifies whether to skip publish for modified entries.")]
        public bool NoPublish { get; set; } = false;

        [CommandOption("--chunk-size")]
        [Description("Specifies published entries chunk size. Default is 100.")]
        public int ChunkSize { get; set; } = 100;

        [CommandOption("--max-call-limit")]
        [Description("Specifies maximum limit of chunks sent at a time. Default is 5.")]
        public int MaxCallLimit { get; set; } = 5;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        settings.ContentTypeId = await ResolveContentTypeId(settings.ContentTypeId) ??
            throw new CliException("You need to specify a content type to publish entries for.");

        var contentType = await GetContentTypeOrThrowError(settings.ContentTypeId);

        if (!ConfirmWithPromptChallenge($"{"PUBLISH"} all entries in '{settings.ContentTypeId}'"))
        {
            return -1;
        }

        await PerformBulkOperations(
            [
                new PublishBulkAction(ContentfulConnection, _httpClient)
                    .WithContentType(contentType)
                    .WithContentLocales(await ContentfulConnection.GetContentLocalesAsync())
                    .WithVerbosity(settings.Verbosity)
                    .WithApplyChanges(!settings.NoPublish)
                    .WithPublishChunkSize(settings.ChunkSize)
                    .WithBulkActionCallLimit(settings.MaxCallLimit)
            ]
        );

        _console.WriteBlankLine();

        _console.WriteNormalWithHighlights($"Completed {"PUBLISH"} of all '{settings.ContentTypeId}' entries.", Globals.StyleHeading);

        return 0;
    }
}