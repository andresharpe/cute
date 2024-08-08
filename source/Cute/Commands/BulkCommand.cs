using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

public sealed class BulkCommand : LoggedInCommand<BulkCommand.Settings>
{
    private readonly BulkActionExecutor _bulkActionExecutor;

    public BulkCommand(IConsoleWriter console, ILogger<InfoCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings,
        BulkActionExecutor bulkActionExecutor)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _bulkActionExecutor = bulkActionExecutor;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--content-type")]
        [Description("Specifies the content type to purge data from")]
        public string ContentType { get; set; } = null!;

        [CommandOption("-b|--bulk-action")]
        [Description("Specifies the bulk action to perform")]
        public BulkAction BulkAction { get; set; } = BulkAction.Publish;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _ = await base.ExecuteAsync(context, settings);

        var contentType = settings.ContentType;
        var action = settings.BulkAction.ToString().ToUpper();

        int challenge = new Random().Next(10, 100);

        var continuePrompt = new TextPrompt<int>($"[{Globals.StyleAlert.Foreground}]About to {action} all '{contentType}' entries. Enter '{challenge}' to continue:[/]")
            .PromptStyle(Globals.StyleAlertAccent);

        _console.WriteRuler();
        _console.WriteBlankLine();
        _console.WriteAlertAccent("WARNING!");
        _console.WriteBlankLine();

        var response = _console.Prompt(continuePrompt);

        if (challenge != response)
        {
            _console.WriteBlankLine();
            _console.WriteAlert("The response does not match the challenge. Aborting.");
            return -1;
        }

        _console.WriteBlankLine();

        await _bulkActionExecutor
            .WithContentType(contentType)
            .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
            .Execute(settings.BulkAction);

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"Completed {action} of all entries of '{contentType}'.", Globals.StyleHeading);

        return 0;
    }
}