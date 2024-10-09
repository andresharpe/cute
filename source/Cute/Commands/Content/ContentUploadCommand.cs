using Cute.Commands.BaseCommands;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.InputAdapters.FileAdapters;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using static Cute.Commands.Content.ContentUploadCommand;

namespace Cute.Commands.Content;

public class ContentUploadCommand(IConsoleWriter console, ILogger<ContentUploadCommand> logger,
    AppSettings appSettings, HttpClient httpClient)
    : BaseLoggedInCommand<Settings>(console, logger, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : ContentCommandSettings
    {
        [CommandOption("-p|--path <PATH>")]
        [Description("The local path to the file containing the data to sync")]
        public string Path { get; set; } = default!;

        [CommandOption("-f|--format <FORMAT>")]
        [Description("The format of the file specified in '--path' (Excel/CSV/TSV/JSON/YAML)")]
        public InputFileFormat? Format { get; set; }

        [CommandOption("-m|--match-field <NAME>")]
        [Description("The optional name of the field to match in addition to the entry id.")]
        public string? MatchField { get; set; } = null!;

        [CommandOption("-a|--apply")]
        [Description("Apply and publish all the calculated changes. The default behaviour is to only list the detected changes.")]
        public bool Apply { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.Path))
        {
            return ValidationResult.Error($"Path not found - {settings.Path}");
        }

        if (settings.Format == null)
        {
            var ext = new FileInfo(settings.Path).Extension.ToLowerInvariant();

            settings.Format ??= ext switch
            {
                ".xlsx" => InputFileFormat.Excel,
                ".csv" => InputFileFormat.Csv,
                ".tsv" => InputFileFormat.Tsv,
                ".json" => InputFileFormat.Json,
                ".yaml" => InputFileFormat.Yaml,
                ".yml" => InputFileFormat.Yaml,
                _ => throw new CliException($"Could not determine the format for {settings.Path}. Use the --format switch to specify the file format.")
            };
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        if (settings.Format == null) return -1;

        settings.ContentTypeId = await ResolveContentTypeId(settings.ContentTypeId) ??
            throw new CliException("You need to specify a content type to upload.");

        var contentType = await GetContentTypeOrThrowError(settings.ContentTypeId);

        if (!ConfirmWithPromptChallenge($"CREATE and UPDATE entries in '{settings.ContentTypeId}'"))
        {
            return -1;
        }

        var contentLocales = await ContentfulConnection.GetContentLocalesAsync();

        var fileFormat = settings.Format.Value;

        if (settings.MatchField is not null)
        {
            var field = contentType.Fields.FirstOrDefault(f => f.Id.Equals(settings.MatchField, StringComparison.OrdinalIgnoreCase))
                ?? throw new CliException($"The match field '{settings.MatchField}' was not found on content type '{contentType.SystemProperties.Id}'. Did you mean '{contentType.BestFieldMatch(settings.MatchField)}'?");

            settings.MatchField = field.Id;
        }
        await PerformBulkOperations(
        [

            new UpsertBulkAction(ContentfulConnection, _httpClient)
                .WithContentType(contentType)
                .WithContentLocales(contentLocales)
                .WithNewEntries(
                    FileInputAdapterFactory.Create(
                        fileFormat,
                        settings.ContentTypeId,
                        settings.Path
                    ))
                .WithMatchField(settings.MatchField)
                .WithApplyChanges(settings.Apply)
                .WithVerbosity(settings.Verbosity),

            new PublishBulkAction(ContentfulConnection, _httpClient)
                .WithContentType(contentType)
                .WithContentLocales(contentLocales)
                .WithVerbosity(settings.Verbosity)

            ]
        );

        return 0;
    }
}