using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Extensions;
using Cute.Services;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Type;

public class TypeDeleteCommand(IConsoleWriter console, ILogger<TypeDeleteCommand> logger, AppSettings appSettings,
    HttpClient httpClient)
    : BaseLoggedInCommand<TypeDeleteCommand.Settings>(console, logger, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-c|--content-type-id <ID>")]
        [Description("Specifies the content type id to be deleted.")]
        public string ContentTypeId { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        ContentType contentType = await GetContentTypeOrThrowError(settings.ContentTypeId);
        var contentfulEnvironment = await ContentfulConnection.GetDefaultEnvironmentAsync();

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"{settings.ContentTypeId} found in environment {contentfulEnvironment.Id()}", Globals.StyleHeading);

        if (!ConfirmWithPromptChallenge($"destroy all '{settings.ContentTypeId}' entries in {contentfulEnvironment.Id()}"))
        {
            return -1;
        }

        _console.WriteBlankLine();

        await PerformBulkOperations(
            [
                new DeleteBulkAction(ContentfulConnection, _httpClient)
                    .WithContentType(contentType)
                    .WithContentLocales(await ContentfulConnection.GetContentLocalesAsync())
                    .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
            ]
        );

        await ContentfulConnection.DeleteContentTypeAsync(contentType);

        _console.WriteNormalWithHighlights($"Deleted {settings.ContentTypeId} in {contentfulEnvironment.Id()}", Globals.StyleHeading);

        return 0;
    }
}