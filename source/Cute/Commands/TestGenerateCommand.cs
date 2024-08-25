using Cute.Config;
using Cute.Constants;
using Cute.Lib.CommandRunners;
using Cute.Lib.CommandRunners.Filters;
using Cute.Lib.Contentful;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

public sealed class TestGenerateCommand : LoggedInCommand<TestGenerateCommand.Settings>
{
    private readonly ILogger<GenerateCommand> _logger;
    private readonly AzureTranslator _translator;
    private readonly GenerateCommandRunner _generateCommandRunner;

    public TestGenerateCommand(IConsoleWriter console, ILogger<GenerateCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings, AzureTranslator translator,
        GenerateCommandRunner generateCommandRunner)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _logger = logger;
        _translator = translator;
        _generateCommandRunner = generateCommandRunner;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-i|--prompt-id")]
        [Description("The id of the Contentful prompt entry to generate prompts from.")]
        public string PromptId { get; set; } = default!;

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
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        var displayActions = new CommandRunnerDisplayActions()
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

        var runnerResult = await _generateCommandRunner.GenerateContent(settings.PromptId,
            displayActions,
            testOnly: true,
            dataFilter: dataFilter,
            modelNames: models
        );

        if (runnerResult.Result == RunnerResult.Error)
        {
            throw new CliException(runnerResult.Message);
        }

        return 0;
    }
}