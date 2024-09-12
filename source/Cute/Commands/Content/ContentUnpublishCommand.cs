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
using static Cute.Commands.Content.ContentUnpublishCommand;

namespace Cute.Commands.Content;

public class ContentUnpublishCommand(IConsoleWriter console, ILogger<ContentUnpublishCommand> logger, ContentfulConnection contentfulConnection,
    AppSettings appSettings, HttpClient httpClient)
    : BaseLoggedInCommand<Settings>(console, logger, contentfulConnection, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-c|--content-type-id <ID>")]
        [Description("The Contentful content type id.")]
        public string ContentTypeId { get; set; } = default!;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        settings.ContentTypeId = ResolveContentTypeId(settings.ContentTypeId) ??
            throw new CliException("You need to specify a content type to unpublish entries for.");

        var contentType = GetContentTypeOrThrowError(settings.ContentTypeId);

        if (!ConfirmWithPromptChallenge($"{"UNPUBLISH"} all entries in '{settings.ContentTypeId}'"))
        {
            return -1;
        }

        await PerformBulkOperations(
            [
                new UnpublishBulkAction(_contentfulConnection, _httpClient)
                    .WithContentType(contentType)
                    .WithContentLocales(ContentLocales)
                    .WithVerbosity(settings.Verbosity)
            ]
        );

        _console.WriteBlankLine();

        _console.WriteNormalWithHighlights($"Completed {"UNPUBLISH"} of all '{settings.ContentTypeId}' entries.", Globals.StyleHeading);

        return 0;
    }
}