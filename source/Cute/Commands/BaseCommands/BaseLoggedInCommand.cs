using ClosedXML;
using Contentful.Core.Models;
using Cute.Commands.Content;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Contentful.CommandModels.ContentSyncApi;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Services;
using Cute.UiComponents;
using FuzzySharp;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;
using Table = Spectre.Console.Table;
using Text = Spectre.Console.Text;

namespace Cute.Commands.BaseCommands;

public abstract class BaseLoggedInCommand<TSettings>(IConsoleWriter console, ILogger logger,
    AppSettings appSettings)
    : AsyncCommand<TSettings>
    where TSettings : LoggedInSettings
{
    protected readonly IConsoleWriter _console = console;

    protected readonly ILogger _logger = logger;

    private ContentfulConnection _contentfulConnection = null!;

    private readonly AppSettings _appSettings = appSettings;

    private TSettings _settings = null!;

    private bool _force = false;

    private IEnumerable<string> _contextIds = [];

    public AppSettings AppSettings => _appSettings;
    public ContentfulConnection ContentfulConnection => _contentfulConnection;

    public override ValidationResult Validate(CommandContext context, TSettings settings)
    {
        return base.Validate(context, settings);
    }

    public abstract Task<int> ExecuteCommandAsync(CommandContext context, TSettings settings);

    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
        if (settings is not LoggedInSettings loggedInSettings)
        {
            throw new CliException(
                $"Unexpected settings type ('{settings.GetType().Name}' does not inherit from 'LoggedInSettings'"
            );
        }

        _settings = settings;

        _console.Logger = _logger;

        var options = _appSettings.GetContentfulOptions();

        options.SpaceId = settings.SpaceId ?? options.SpaceId;
        options.Environment = settings.EnvironmentId ?? options.Environment;

        _contentfulConnection = new ContentfulConnection.Builder()
            .WithHttpClient(new HttpClient())
            .WithOptions(options)
            .Build();

        loggedInSettings.SpaceId ??= options.SpaceId;

        loggedInSettings.EnvironmentId ??= options.Environment;

        _force = settings.Force;

        await DisplaySettings(context, settings);

        var stopWatch = Stopwatch.StartNew();

        var result = await ExecuteCommandAsync(context, settings);

        stopWatch.Stop();

        var elapsed = stopWatch.Elapsed;

        _console.WriteBlankLine();

        _console.WriteNormalWithHighlights(
            $"Elapsed time: {elapsed.Days}d {elapsed.Hours}h {elapsed.Minutes}m {elapsed.Seconds}.{elapsed.Milliseconds}s",
            Globals.StyleHeading);

        using var process = System.Diagnostics.Process.GetCurrentProcess();

        long peakMemory = process.PeakWorkingSet64;

        _console.WriteNormalWithHighlights(
            $"Memory used : {peakMemory / (1024 * 1024)} MB",
            Globals.StyleHeading);

        return result;
    }

    protected IReadOnlyDictionary<string, object?> GetOptions(TSettings settings)
    {
        var settingsType = settings.GetType();

        var properties = settingsType.GetProperties();

        var returnDict = new Dictionary<string, object?>();

        foreach (var prop in properties)
        {
            if (prop.Name.Contains("password", StringComparison.OrdinalIgnoreCase)
                || prop.Name.Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                // don't output these...
                continue;
            }

            var attr = prop.GetAttributes<CommandOptionAttribute>()
                .FirstOrDefault()?
                .LongNames.ToArray();

            if (attr != null && attr.Length > 0)
            {
                var option = attr[0];
                if (option != null)
                {
                    var value = prop.GetValue(settings);
                    returnDict.Add(option, value);
                }
            }
        }
        return returnDict;
    }

    private async Task DisplaySettings(CommandContext context, TSettings settings)
    {
        var optionTable = new Table().NoBorder();

        var showTable = false;

        var locales = await _contentfulConnection.GetLocalesAsync();

        var defaultLocale = await _contentfulConnection.GetDefaultLocaleAsync();

        if (settings is ContentCommandSettings contentSettings)
        {
            if (contentSettings.Locales is null || contentSettings.Locales.Length == 0)
            {
                contentSettings.Locales = locales
                    .Select(l => l.Code)
                    .ToArray();
            }
            else
            {
                var validatedLocales = contentSettings.Locales
                    .ToHashSet()
                    .Select(l => locales.FirstOrDefault(c => c.Code.Equals(l, StringComparison.OrdinalIgnoreCase)))
                    .Where(l => l is not null)
                    .Where(l => !l!.Equals(defaultLocale.Code))
                    .Select(l => l!.Code)
                    .ToList();

                validatedLocales.Insert(0, defaultLocale.Code);

                contentSettings.Locales = [.. validatedLocales];
            }
        }

        var space = await _contentfulConnection.GetDefaultSpaceAsync();

        _logger.LogInformation("Logging into Contentful space {name} (id: {space})", space.Name, space.SystemProperties.Id);

        var environment = await _contentfulConnection.GetDefaultEnvironmentAsync();

        _logger.LogInformation("Using environment {environment}", environment.SystemProperties.Id);

        var user = await _contentfulConnection.GetCurrentUserAsync();

        _logger.LogInformation("Logged in as user {name} (id: {id})", user.Email, user.SystemProperties.Id);

        _logger.LogInformation("Starting command {command}", context.Name);

        optionTable.AddColumn(new TableColumn(new Text("Option", Globals.StyleSubHeading)).Alignment(Justify.Right));
        optionTable.AddColumn(new TableColumn(new Text("", Globals.StyleSubHeading)));
        optionTable.AddColumn(new TableColumn(new Text("Value", Globals.StyleSubHeading)));

        var options = GetOptions(settings);

        foreach (var (option, value) in options)
        {
            var displayValue = value;

            if (value is string[] stringArray)
            {
                displayValue = string.Join(',', stringArray.Select(e => $"'{e}'"));
            }

            _logger.LogInformation("Command option: {option} = {value}", option, displayValue);
            optionTable.AddRow(
                new Markup($"--{option}", Globals.StyleDim),
                new Markup($"=", Globals.StyleDim),
                new Markup($"{displayValue?.ToString().EscapeMarkup()}", Globals.StyleNormal)
            );
            showTable = true;
        }

        _console.Write(new Markup("  " + string.Join(' ', context.Arguments.Select(s => s.EscapeMarkup())), Globals.StyleAlertAccent));

        _console.WriteLine();

        _console.WriteBlankLine();

        if (showTable)
        {
            _console.Write(optionTable);

            _console.WriteBlankLine();
        }

        await CreateCuteContentTypesIfNotExists();
    }

    public async Task<ContentType> GetContentTypeOrThrowError(string contentTypeId, string? intent = null)
    {
        var contentTypes = await _contentfulConnection.GetContentTypesAsync();

        return contentTypes.FirstOrDefault(c => c.SystemProperties.Id == contentTypeId) ??
            throw new CliException($"No content type with id '{contentTypeId}' found. {intent}");
    }

    public async Task<bool> CreateContentTypeIfNotExist(ContentType contentType)
    {
        var contentTypeId = contentType.SystemProperties.Id;

        var contentTypes = await _contentfulConnection.GetContentTypesAsync();

        if (contentTypes.Any(c => c.SystemProperties.Id == contentTypeId))
        {
            return false;
        }

        await _contentfulConnection.CreateContentTypeAsync(contentType);

        return true;
    }

    private async Task CreateCuteContentTypesIfNotExists()
    {
        ContentType[] cuteTypes = [
            CuteDataQueryContentType.Instance(),
            CuteLanguageContentType.Instance(),
            CuteContentSyncApiContentType.Instance(),
            CuteContentGenerateContentType.Instance(),
            CuteContentGenerateBatchContentType.Instance()
        ];

        var contentTypeIds = (await _contentfulConnection.GetContentTypesAsync())
            .Select(c => c.SystemProperties.Id)
            .ToHashSet();

        var missingTypes = cuteTypes
            .Where(c => !contentTypeIds.Contains(c.SystemProperties.Id))
            .Select(async c =>
            {
                await _contentfulConnection.CreateContentTypeAsync(c);
                _console.WriteNormalWithHighlights($"Created content type {c.SystemProperties.Id}...", Globals.StyleHeading);
                return c;
            });
    }

    public async Task<string?> ResolveContentTypeId(string? suppliedContentType)
    {
        var promptContentTypes = new SelectionPrompt<string>()
            .Title($"[{Globals.StyleNormal.Foreground}]Select a content type:[/]")
            .PageSize(10)
            .MoreChoicesText($"[{Globals.StyleDim.ToMarkup()}](Move up and down to reveal more environments)[/]")
            .HighlightStyle(Globals.StyleSubHeading);

        promptContentTypes.SearchEnabled = true;

        var contentTypes = await _contentfulConnection.GetContentTypesAsync();

        if (suppliedContentType is not null)
        {
            var exactMatch = contentTypes
                .FirstOrDefault(t => t.SystemProperties.Id.Equals(suppliedContentType))?
                .SystemProperties.Id!;

            if (exactMatch is not null) return exactMatch;

            var caseInsensitiveMatch =
                contentTypes
                    .FirstOrDefault(t => t.SystemProperties.Id.Equals(suppliedContentType, StringComparison.OrdinalIgnoreCase))?
                    .SystemProperties.Id!;

            if (caseInsensitiveMatch is not null) return caseInsensitiveMatch;

            promptContentTypes.AddChoices(
                contentTypes
                    .Select(c => c.SystemProperties.Id)
                    .OrderByDescending(e => Fuzz.PartialRatio(suppliedContentType, e))
                    .Select(e => $"[{Globals.StyleDim.Foreground}]{e}[/]")
            );
        }
        else
        {
            promptContentTypes.AddChoices(
                contentTypes
                    .Select(c => c.SystemProperties.Id)
                    .OrderBy(e => e)
                    .Select(e => $"[{Globals.StyleDim.Foreground}]{e}[/]")
            );
        }

        return Markup.Remove(_console.Prompt(promptContentTypes));
    }

    public bool ConfirmWithPromptChallenge(FormattableString actionDescription)
    {
        if (_force)
        {
            return true;
        }

        int challenge = new Random().Next(10, 100);

        FormattableString confirmation = $"Enter '{challenge}' to continue:";

        var style = Globals.StyleAlert;
        var styleAccent = Globals.StyleAlertAccent;

        var continuePrompt = new TextPrompt<int>($"[{style.Foreground}]About to {_console.FormatToMarkup(actionDescription, style, styleAccent)}. {_console.FormatToMarkup(confirmation, style, styleAccent)}[/]")
            .PromptStyle(Globals.StyleAlertAccent);

        _console.WriteRuler();
        _console.WriteBlankLine();
        _console.WriteAlert("WARNING!");
        _console.WriteBlankLine();

        var response = _console.Prompt(continuePrompt);

        if (challenge != response)
        {
            _console.WriteBlankLine();
            _console.WriteAlert("The response does not match the challenge. Aborting.");
            return false;
        }

        _console.WriteBlankLine();

        return true;
    }

    public async Task PerformBulkOperations(IBulkAction[] executors, string? processName = null)
    {
        if (ConsoleWriter.EnableConsole)
        {
            await PerformBulkOperationsWithConsole(executors, processName);
        }
        else
        {
            await PerformBulkOperationsWithoutConsole(executors, processName);
        }
    }

    public async Task PerformBulkOperationsWithConsole(IBulkAction[] executors, string? processName)
    {
        var task = new List<List<ProgressTask>>(executors.Length);

        var taskRetries = new List<List<ProgressTask>>(executors.Length);

        await ProgressBars.Instance()
         .AutoClear(false)
         .StartAsync(async ctx =>
         {
             for (var i = 0; i < executors.Length; i++)
             {
                 var progressInfo = executors[i].ActionProgressIndicators();

                 task.Add([]);
                 taskRetries.Add([]);

                 for (var j = 0; j < progressInfo.Count; j++)
                 {
                     var initialMessage = processName == null
                         ? progressInfo[j].Intent
                         : processName + ": " + progressInfo[j].Intent;

                     task[i].Add(ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.GlobeWithMeridians} {initialMessage}...[/]"));
                     task[i][j].Value = 0;
                     task[i][j].MaxValue = 1;
                     task[i][j].IsIndeterminate = true;

                     taskRetries[i].Add(ctx.AddTask($"[{Globals.StyleDim.Foreground}]   0 Retries.[/]"));
                     taskRetries[i][j].Value = 0;
                     taskRetries[i][j].MaxValue = 1;
                     taskRetries[i][j].IsIndeterminate = true;
                 }
             }
             for (var i = 0; i < executors.Length; i++)
             {
                 var progressInfo = executors[i].ActionProgressIndicators();
                 var progressBars = progressInfo.Count;
                 var progressBarActions = new Action<BulkActionProgressEvent>[progressBars];

                 for (var j = 0; j < progressBars; j++)
                 {
                     task[i][j].IsIndeterminate = false;
                     task[i][j].StartTask();

                     taskRetries[i][j].IsIndeterminate = false;
                     taskRetries[i][j].StartTask();

                     var ii = i;
                     var jj = j;

                     progressBarActions[j] = (n) =>
                     {
                         task[ii][jj].MaxValue = Math.Max(n.Steps ?? task[ii][jj].MaxValue, 1);
                         task[ii][jj].Value = n.Step ?? task[ii][jj].Value;
                         taskRetries[ii][jj].MaxValue = task[ii][jj].MaxValue;

                         if (n.Message is not null)
                         {
                             task[ii][jj].Description = $"[{Globals.StyleNormal.Foreground}]{Emoji.Known.GlobeWithMeridians} {_console.FormatToMarkup(n.Message)}[/]";
                             ctx.Refresh();
                         }
                         if (n.Error is not null)
                         {
                             taskRetries[ii][jj].Value++;
                             taskRetries[ii][jj].Description = $"[{Globals.StyleAlert.Foreground}]   {taskRetries[ii][jj].Value} Retries [{Globals.StyleAlertAccent.Foreground}]{Markup.Escape(n.Error.ToString().Snip(60))}[/][/]";
                             ctx.Refresh();
                         }

                         if (task[ii][jj].Value == task[ii][jj].MaxValue)
                         {
                             task[ii][jj].StopTask();
                             taskRetries[ii][jj].StopTask();
                         }
                         else
                         {
                             task[ii][jj].StartTask();
                             taskRetries[ii][jj].StartTask();
                         }
                     };
                 }

                 _contextIds = await executors[i]
                     .WithContentfulConnection(_contentfulConnection)
                     .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                     .WithVerbosity(_settings.Verbosity)
                     .WithSessionEntryIds(_contextIds)
                     .ExecuteAsync(progressBarActions);

                 ctx.Refresh();
             }
         });
    }

    public async Task PerformBulkOperationsWithoutConsole(IBulkAction[] executors, string? processName)
    {
        for (var i = 0; i < executors.Length; i++)
        {
            var progressInfo = executors[i].ActionProgressIndicators();

            var maxIntentLength = progressInfo.Max(p => p.Intent.Length);

            var progressUpdaters = new Action<BulkActionProgressEvent>[progressInfo.Count];

            for (var j = 0; j < progressInfo.Count; j++)
            {
                var progressMessage = progressInfo[j].Intent;

                progressUpdaters[j] = e =>
                {
                    if (e.Message is not null && e.Step is not null && e.Steps is not null)
                        _logger.LogInformation("{processName} > {progressMessage}: {message} ({step}/{steps})", processName, progressMessage, e.Message.ToString(), e.Step, e.Steps);
                    else if (e.Message is not null)
                        _logger.LogInformation("{processName} > {progressMessage}: {message}", processName, progressMessage, e.Message.ToString());

                    if (e.Error is not null)
                        _logger.LogError("{processName} > {progressMessage}: {message}", processName, progressMessage, e.Error.ToString());
                };
            }

            _contextIds = await executors[i]
                .WithContentfulConnection(_contentfulConnection)
                .WithDisplayAction(m => _logger.LogDebug("{displayMessage}", m.ToString()))
                .WithVerbosity(_settings.Verbosity)
                .WithSessionEntryIds(_contextIds)
                .ExecuteAsync(progressUpdaters);
        }
    }
}