using ClosedXML;
using Contentful.Core;
using Contentful.Core.Configuration;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Exceptions;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using Table = Spectre.Console.Table;
using Text = Spectre.Console.Text;

namespace Cute.Commands;

public abstract class LoggedInCommand<TSettings> : AsyncCommand<TSettings> where TSettings : CommandSettings
{
    protected readonly IConsoleWriter _console;

    protected readonly IPersistedTokenCache _tokenCache;

    private readonly ILogger _logger;

    private readonly ContentfulManagementClient _contentfulManagementClient;
    protected ContentfulManagementClient ContentfulManagementClient => _contentfulManagementClient;

    private readonly ContentfulClient _contentfulClient;
    protected ContentfulClient ContentfulClient => _contentfulClient;

    private readonly ContentfulOptions _contentfulOptions;

    protected string ContentfulSpaceId => _contentfulOptions.SpaceId;

    protected string ContentfulEnvironmentId => _contentfulOptions.Environment;

    private User _contentfulUser;

    protected User ContentfulUser => _contentfulUser;

    private Space _contentfulSpace;

    protected Space ContentfulSpace => _contentfulSpace;

    protected readonly AppSettings _appSettings;

    public LoggedInCommand(IConsoleWriter console, IPersistedTokenCache tokenCache, ILogger logger)
    {
        _console = console;

        _console.Logger = logger;

        _tokenCache = tokenCache;

        _logger = logger;

        _appSettings = _tokenCache.LoadAsync(Globals.AppName).Result
            ?? throw new CliException($"Use '{Globals.AppName} login' to connect to contentful first.");

        if (string.IsNullOrEmpty(_appSettings.ContentfulManagementApiKey))
        {
            throw new CliException($"Invalid management api key. Use '{Globals.AppName} login' to connect to contentful first.");
        }

        if (string.IsNullOrEmpty(_appSettings.ContentfulDeliveryApiKey))
        {
            throw new CliException($"Invalid delivery api key. Use '{Globals.AppName} login' to connect to contentful first.");
        }

        _contentfulOptions = new ContentfulOptions()
        {
            ManagementApiKey = _appSettings.ContentfulManagementApiKey,
            SpaceId = _appSettings.ContentfulDefaultSpace,
            DeliveryApiKey = _appSettings.ContentfulDeliveryApiKey,
            PreviewApiKey = _appSettings.ContentfulPreviewApiKey,
            Environment = _appSettings.ContentfulDefaultEnvironment,
            ResolveEntriesSelectively = true,
        };

        _contentfulClient = new ContentfulClient(new HttpClient(), _contentfulOptions);

        if (ContentfulClient is null)
        {
            throw new CliException("Could not log into the Contentful Delivery API.");
        }

        _contentfulManagementClient = new ContentfulManagementClient(new HttpClient(), _contentfulOptions);

        if (_contentfulManagementClient is null)
        {
            throw new CliException("Could not log into the Contentful Management API.");
        }

        _contentfulSpace = new Space();

        _contentfulUser = new User();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
        await DisplaySettings(context, settings);

        return 0;
    }

    private async Task DisplaySettings(CommandContext context, TSettings settings)
    {
        var settingsType = settings.GetType();

        var properties = settingsType.GetProperties();

        var table = new Table().NoBorder();

        _contentfulUser = await _contentfulManagementClient.GetCurrentUser();

        _contentfulSpace = await _contentfulManagementClient.GetSpace(ContentfulSpaceId);

        _logger.LogInformation("Logging into Contentful space {name} (id: {space})", _contentfulSpace.Name, ContentfulSpaceId);

        _logger.LogInformation("Using environment {environment}", ContentfulEnvironmentId);

        _logger.LogInformation("Logged in as user {name} (id: {id})", _contentfulUser.Email, _contentfulUser.SystemProperties.Id);

        _logger.LogInformation("Starting command {command}", context.Name);

        table.AddColumn(new TableColumn(new Text("Option", Globals.StyleSubHeading)).Alignment(Justify.Right));
        table.AddColumn(new TableColumn(new Text("", Globals.StyleSubHeading)));
        table.AddColumn(new TableColumn(new Text("Value", Globals.StyleSubHeading)));

        foreach (var prop in properties)
        {
            var attr = prop.GetAttributes<CommandOptionAttribute>()
                .FirstOrDefault()?
                .LongNames.ToArray();

            if (attr != null && attr.Length > 0)
            {
                var option = attr[0];
                if (option != null)
                {
                    var value = prop.GetValue(settings);
                    _logger.LogInformation("Command option: {option} = {value}", option, value);
                    table.AddRow(
                        new Markup($"--{option}", Globals.StyleDim),
                        new Markup($"=", Globals.StyleDim),
                        new Markup($"{value}", Globals.StyleNormal)
                    );
                }
            }
        }

        _console.Write(new Markup("  " + string.Join(' ', context.Arguments), Globals.StyleAlertAccent));

        _console.WriteLine();

        _console.WriteBlankLine();

        _console.Write(table);

        _console.WriteBlankLine();
    }
}