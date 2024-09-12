﻿using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Exceptions;
using Cute.Lib.InputAdapters;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using static Cute.Commands.Content.ContentEditCommand;

namespace Cute.Commands.Content;

public class ContentEditCommand(IConsoleWriter console, ILogger<ContentEditCommand> logger, ContentfulConnection contentfulConnection,
    AppSettings appSettings, HttpClient httpClient)
    : BaseLoggedInCommand<Settings>(console, logger, contentfulConnection, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-c|--content-type-id <ID>")]
        [Description("The Contentful content type id.")]
        public string ContentTypeId { get; set; } = default!;

        [CommandOption("-l|--locale <CODE>")]
        [Description("The locale code (eg. 'en') to apply the command to.")]
        public string Locale { get; set; } = default!;

        [CommandOption("-f|--field")]
        [Description("The field to update.")]
        public string[] Fields { get; set; } = null!;

        [CommandOption("-r|--replace")]
        [Description("The value to update it with. Can contain an expression.")]
        public string[] Values { get; set; } = null!;

        [CommandOption("-a|--apply")]
        [Description("Apply and publish all the required edits. The default behaviour is to only list the detected changes.")]
        public bool Apply { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        settings.Locale ??= DefaultLocaleCode;

        settings.Locale = settings.Locale.ToLower();

        if (settings.Fields.Length != settings.Values.Length)
        {
            return ValidationResult.Error($"Mismatch in field ({settings.Fields.Length}) and value ({settings.Values.Length}) count.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        settings.ContentTypeId = ResolveContentTypeId(settings.ContentTypeId) ??
            throw new CliException("You need to specify a content type to edit.");

        var contentType = GetContentTypeOrThrowError(settings.ContentTypeId);

        var matchedFields = contentType.Fields
            .Select(f => f.Id)
            .Intersect(settings.Fields)
            .ToHashSet();

        var missingFields = settings.Fields
            .Where(f => !matchedFields.Contains(f))
            .ToArray();

        if (missingFields.Length > 0)
        {
            throw new CliException($"The field(s) named '{string.Join(',', missingFields)}' not in content type '{settings.ContentTypeId}'");
        }

        if (!Locales.Any(l => l.Code.Equals(settings.Locale)))
        {
            throw new CliException($"The locale '{settings.Locale}' was not found.");
        }

        if (settings.Apply && !ConfirmWithPromptChallenge($"{"EDIT"} all entries in '{settings.ContentTypeId}'"))
        {
            return -1;
        }

        var contentLocales = ContentLocales;

        await PerformBulkOperations([

            new UpsertBulkAction(_contentfulConnection, _httpClient)
                .WithContentType(contentType)
                .WithContentLocales(contentLocales)
                .WithNewEntries(
                    new ReplaceFieldsInputAdapter(
                        settings.Locale,
                        contentLocales,
                        settings.Fields,
                        settings.Values,
                        contentType,
                        _contentfulConnection
                    ))
                .WithApplyChanges(settings.Apply)
                .WithVerbosity(settings.Verbosity),

            new PublishBulkAction(_contentfulConnection, _httpClient)
                .WithContentType(contentType)
                .WithContentLocales(contentLocales)
                .WithVerbosity(settings.Verbosity)
        ]);

        return 0;
    }
}