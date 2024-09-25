﻿using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.RateLimiters;
using Cute.Services;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Type;

public class TypeDeleteCommand(IConsoleWriter console, ILogger<TypeDeleteCommand> logger, ContentfulConnection contentfulConnection,
    AppSettings appSettings, HttpClient httpClient) : BaseLoggedInCommand<TypeDeleteCommand.Settings>(console, logger, contentfulConnection, appSettings)
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
        ContentType contentType = GetContentTypeOrThrowError(settings.ContentTypeId);
        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"{settings.ContentTypeId} found in environment {ContentfulEnvironmentId}", Globals.StyleHeading);

        if (!ConfirmWithPromptChallenge($"destroy all '{settings.ContentTypeId}' entries in {ContentfulEnvironmentId}"))
        {
            return -1;
        }

        _console.WriteBlankLine();

        await PerformBulkOperations(
            [
                new DeleteBulkAction(_contentfulConnection, _httpClient)
                    .WithContentType(contentType)
                    .WithContentLocales(ContentLocales)
                    .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
            ]
        );

        await RateLimiter.SendRequestAsync(() => ContentfulManagementClient.DeactivateContentType(settings.ContentTypeId));

        await RateLimiter.SendRequestAsync(() => ContentfulManagementClient.DeleteContentType(settings.ContentTypeId));

        _console.WriteNormalWithHighlights($"Deleted {settings.ContentTypeId} in {ContentfulEnvironmentId}", Globals.StyleHeading);

        return 0;
    }
}