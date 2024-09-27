using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Contentful.BulkActions.Models;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using static Cute.Commands.Content.ContentGenerateTestCommand;

namespace Cute.Commands.Content;

public class ContentGenerateTestCommand(IConsoleWriter console, ILogger<ContentGenerateCommand> logger,
    AppSettings appSettings,
    GenerateBulkAction generateBulkAction)
    : BaseLoggedInCommand<Settings>(console, logger, appSettings)
{
    private readonly GenerateBulkAction _generateBulkAction = generateBulkAction;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-k|--key")]
        [Description("The key of the 'cuteContentGenerate' entry.")]
        public string Key { get; set; } = default!;

        [CommandOption("-f|--field-id")]
        [Description("The field id to filter on.")]
        public string FieldId { get; set; } = default!;

        [CommandOption("-o|--comparison-operation")]
        [Description("The comparison operator to apply to the field.")]
        public ComparisonOperation Operation { get; set; } = default!;

        [CommandOption("-v|--field-value")]
        [Description("The field value to filter on.")]
        public string FieldValue { get; set; } = default!;

        [CommandOption("-m|--deployment-models")]
        [Description("The deployment models to test.")]
        public string Models { get; set; } = default!;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.Key))
        {
            return ValidationResult.Error("The key of the of the 'cuteContentGenerate' is required. Specify it with '-k' or '--key'.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var contentMetaType = CuteContentGenerateContentType.Instance();

        if (await CreateContentTypeIfNotExist(contentMetaType))
        {
            _console.WriteNormalWithHighlights($"Created content type {contentMetaType.SystemProperties.Id}...", Globals.StyleHeading);
        }

        var contentMetaTypeId = contentMetaType.SystemProperties.Id;

        var defaultLocale = await ContentfulConnection.GetDefaultLocaleAsync();

        var defaultLocaleCode = defaultLocale.Code;

        var contentLocales = new ContentLocales([defaultLocaleCode], defaultLocaleCode);

        var apiSyncEntry = CuteContentGenerate.GetByKey(ContentfulConnection, settings.Key)
            ?? throw new CliException($"No generate entry '{contentMetaTypeId}' with key '{settings.Key}' was found.");

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

        var dataFilter = new DataFilter(settings.FieldId, settings.Operation, settings.FieldValue);

        string[]? models = null;
        if (settings.Models != null)
        {
            models = settings.Models.Split(',').Select(x => x.Trim()).ToArray();
        }

        _generateBulkAction
            .WithContentTypes(await ContentfulConnection.GetContentTypesAsync())
            .WithContentLocales(contentLocales);

        await _generateBulkAction.GenerateContent(settings.Key,
            displayActions,
            testOnly: true,
            dataFilter: dataFilter,
            modelNames: models
        );

        return 0;
    }
}