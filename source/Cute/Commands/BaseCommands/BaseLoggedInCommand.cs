using ClosedXML;
using Contentful.Core;
using Contentful.Core.Configuration;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Commands.Content;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Contentful.CommandModels.ContentSyncApi;
using Cute.Lib.Contentful.CommandModels.ContentTestData;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.RateLimiters;
using Cute.Services;
using Cute.UiComponents;
using FuzzySharp;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;
using Table = Spectre.Console.Table;
using Text = Spectre.Console.Text;

namespace Cute.Commands.BaseCommands;

public abstract class BaseLoggedInCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : LoggedInSettings
{
    protected readonly IConsoleWriter _console;

    private readonly ILogger _logger;

    protected ContentfulConnection _contentfulConnection;

    private User _contentfulUser = new();

    private Space _contentfulSpace = new();

    private List<Locale> _contentfulLocales = [];

    private Locale _defaultLocale = new();

    private string _defaultLocaleCode = "en";

    private bool _force = false;

    private List<ContentType> _contentTypes = [];
    protected ContentfulManagementClient ContentfulManagementClient => _contentfulConnection.ManagementClient;
    protected ContentfulClient ContentfulClient => _contentfulConnection.DeliveryClient;
    protected ContentfulClient ContentfulPreviewClient => _contentfulConnection.PreviewClient;
    protected ContentfulOptions ContentfulOptions => _contentfulConnection.Options;
    protected string ContentfulSpaceId => ContentfulOptions.SpaceId;
    protected string ContentfulEnvironmentId => ContentfulOptions.Environment;
    protected User ContentfulUser => _contentfulUser;
    protected Space ContentfulSpace => _contentfulSpace;
    protected IEnumerable<Locale> Locales => _contentfulLocales;
    protected IEnumerable<ContentType> ContentTypes => _contentTypes;
    protected ContentLocales ContentLocales => new(Locales.Select(l => l.Code).ToArray(), DefaultLocaleCode);
    protected Locale DefaultLocale => _defaultLocale;
    protected string DefaultLocaleCode => _defaultLocaleCode;

    protected readonly AppSettings _appSettings;

    public override ValidationResult Validate(CommandContext context, TSettings settings)
    {
        if (settings is not LoggedInSettings loggedInSettings)
        {
            return ValidationResult.Error(
                $"Unexpected settings type ('{settings.GetType().Name}' does not inherit from 'LoggedInSettings'"
            );
        }

        loggedInSettings.SpaceId ??= ContentfulSpaceId;

        loggedInSettings.EnvironmentId ??= ContentfulEnvironmentId;

        var isChanged = false;

        var connection = new AppSettings()
        {
            ContentfulDefaultEnvironment = _contentfulConnection.Options.Environment,
            ContentfulDefaultSpace = _contentfulConnection.Options.SpaceId,
            ContentfulManagementApiKey = _contentfulConnection.Options.ManagementApiKey,
            ContentfulDeliveryApiKey = _contentfulConnection.Options.DeliveryApiKey,
            ContentfulPreviewApiKey = _contentfulConnection.Options.PreviewApiKey,
        };

        if (loggedInSettings.EnvironmentId != ContentfulEnvironmentId)
        {
            connection.ContentfulDefaultEnvironment = loggedInSettings.EnvironmentId;
            isChanged = true;
        }

        if (loggedInSettings.SpaceId != ContentfulSpaceId)
        {
            connection.ContentfulDefaultSpace = loggedInSettings.SpaceId;
            isChanged = true;
        }

        if (isChanged)
        {
            _contentfulConnection = new ContentfulConnection(new HttpClient(), connection);
        }

        return base.Validate(context, settings);
    }

    public BaseLoggedInCommand(IConsoleWriter console, ILogger logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings)
    {
        _console = console;

        _console.Logger = logger;

        _logger = logger;

        _contentfulConnection = contentfulConnection;

        _appSettings = appSettings;
    }

    public abstract Task<int> ExecuteCommandAsync(CommandContext context, TSettings settings);

    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
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

        _contentfulUser = await RateLimiter.SendRequestAsync(() => ContentfulManagementClient.GetCurrentUser());

        _contentfulSpace = await RateLimiter.SendRequestAsync(() => ContentfulManagementClient.GetSpace(ContentfulSpaceId));

        var locales = await RateLimiter.SendRequestAsync(() => ContentfulManagementClient.GetLocalesCollection());

        _contentfulLocales = [.. locales.OrderBy(c => !c.Default)];

        _defaultLocale = _contentfulLocales[0];

        _defaultLocaleCode = _defaultLocale.Code;

        if (settings is ContentCommandSettings contentSettings)
        {
            if (contentSettings.Locales is null || contentSettings.Locales.Length == 0)
            {
                contentSettings.Locales = _contentfulLocales
                    .Select(l => l.Code)
                    .ToArray();
            }
            else
            {
                var validatedLocales = contentSettings.Locales
                    .ToHashSet()
                    .Select(l => _contentfulLocales.FirstOrDefault(c => c.Code.Equals(l, StringComparison.OrdinalIgnoreCase)))
                    .Where(l => l is not null)
                    .Where(l => !l!.Equals(_defaultLocaleCode))
                    .Select(l => l!.Code)
                    .ToList();

                validatedLocales.Insert(0, _defaultLocaleCode);

                contentSettings.Locales = [.. validatedLocales];
            }
        }

        _logger.LogInformation("Logging into Contentful space {name} (id: {space})", _contentfulSpace.Name, ContentfulSpaceId);

        _logger.LogInformation("Using environment {environment}", ContentfulEnvironmentId);

        _logger.LogInformation("Logged in as user {name} (id: {id})", _contentfulUser.Email, _contentfulUser.SystemProperties.Id);

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
                new Markup($"{displayValue}", Globals.StyleNormal)
            );
            showTable = true;
        }

        _console.Write(new Markup("  " + string.Join(' ', context.Arguments), Globals.StyleAlertAccent));

        _console.WriteLine();

        _console.WriteBlankLine();

        if (showTable)
        {
            _console.Write(optionTable);

            _console.WriteBlankLine();
        }

        _contentTypes = [.. await RateLimiter.SendRequestAsync(() => ContentfulManagementClient.GetContentTypes())];

        await CreateTestContentTypesIfNotExists();
    }

    public string? GetString(Entry<JObject> entry, string key, string? localeCode = null)
    {
        localeCode ??= _defaultLocaleCode;
        return entry.Fields[key]?[localeCode]?.Value<string>();
    }

    public float? GetFloat(Entry<JObject> entry, string key, string? localeCode = null)
    {
        localeCode ??= _defaultLocaleCode;
        return entry.Fields[key]?[localeCode]?.Value<float>();
    }

    public U? GetObject<U>(Entry<JObject> entry, string key, string? localeCode = null)
    {
        localeCode ??= _defaultLocaleCode;
        var token = entry.Fields[key]?[localeCode];

        if (token == null) return default;

        return token.ToObject<U>();
    }

    public ContentType GetContentTypeOrThrowError(string contentTypeId, string? intent = null)
    {
        return ContentTypes.FirstOrDefault(c => c.SystemProperties.Id == contentTypeId) ??
        throw new CliException($"No content type with id '{contentTypeId}' found. {intent}");
    }

    public async Task<bool> CreateContentTypeIfNotExist(ContentType contentType)
    {
        var contentTypeId = contentType.SystemProperties.Id;

        if (ContentTypes.Any(c => c.SystemProperties.Id == contentTypeId))
        {
            return false;
        }

        await RateLimiter.SendRequestAsync(() => contentType.CreateWithId(ContentfulManagementClient));

        _contentTypes.Add(contentType);

        return true;
    }

    private async Task CreateTestContentTypesIfNotExists()
    {
        if (await CreateContentTypeIfNotExist(CuteDataQueryContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type {"cuteDataQuery"}...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(CuteLanguageContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"cuteLanguage"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(CuteContentSyncApiContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"cuteContentSyncApi"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(CuteContentGenerateContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"cuteContentGenerate"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(CuteContentGenerateBatchContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type batch tracker '{"cuteContentGenerateBatch"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(TestUserContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"testUser"}'...", Globals.StyleHeading);
        }
    }

    public string? ResolveContentTypeId(string? suppliedContentType)
    {
        var promptContentTypes = new SelectionPrompt<string>()
            .Title($"[{Globals.StyleNormal.Foreground}]Select a content type:[/]")
            .PageSize(10)
            .MoreChoicesText($"[{Globals.StyleDim.ToMarkup()}](Move up and down to reveal more environments)[/]")
            .HighlightStyle(Globals.StyleSubHeading);

        promptContentTypes.SearchEnabled = true;

        if (suppliedContentType is not null)
        {
            var exactMatch = ContentTypes
                .FirstOrDefault(t => t.SystemProperties.Id.Equals(suppliedContentType))?
                .SystemProperties.Id!;

            if (exactMatch is not null) return exactMatch;

            var caseInsensitiveMatch = ContentTypes
                .FirstOrDefault(t => t.SystemProperties.Id.Equals(suppliedContentType, StringComparison.OrdinalIgnoreCase))?
                .SystemProperties.Id!;

            if (caseInsensitiveMatch is not null) return caseInsensitiveMatch;

            promptContentTypes.AddChoices(ContentTypes
                .Select(c => c.SystemProperties.Id)
                .OrderByDescending(e => Fuzz.PartialRatio(suppliedContentType, e))
                .Select(e => $"[{Globals.StyleDim.Foreground}]{e}[/]")
            );
        }
        else
        {
            promptContentTypes.AddChoices(ContentTypes
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

    public async Task PerformBulkOperations(IBulkAction[] executors)
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
                     var initialMessage = progressInfo[j].Intent;

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

                 await executors[i]
                     .WithContentfulConnection(_contentfulConnection)
                     .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                     .ExecuteAsync(progressBarActions);

                 ctx.Refresh();
             }
         });
    }
}