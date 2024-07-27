using ClosedXML;
using Contentful.Core;
using Contentful.Core.Configuration;
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

    protected readonly ContentfulManagementClient _contentfulManagementClient;

    protected readonly ContentfulClient _contentfulClient;

    protected readonly string _spaceId = string.Empty;

    protected readonly string _environmentId = string.Empty;

    protected User? _user;

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

        _spaceId = _appSettings.ContentfulDefaultSpace;

        _environmentId = _appSettings.ContentfulDefaultEnvironment;

        var contentfulOptions = new ContentfulOptions()
        {
            ManagementApiKey = _appSettings.ContentfulManagementApiKey,
            SpaceId = _appSettings.ContentfulDefaultSpace,
            DeliveryApiKey = _appSettings.ContentfulDeliveryApiKey,
            PreviewApiKey = _appSettings.ContentfulPreviewApiKey,
            Environment = _appSettings.ContentfulDefaultEnvironment,
            ResolveEntriesSelectively = true,
        };

        _contentfulClient = new ContentfulClient(new HttpClient(), contentfulOptions);

        if (_contentfulClient is null)
        {
            throw new CliException("Could not log into the Contentful Delivery API.");
        }

        _contentfulManagementClient = new ContentfulManagementClient(new HttpClient(), contentfulOptions);

        if (_contentfulManagementClient is null)
        {
            throw new CliException("Could not log into the Contentful Management API.");
        }
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

        _user = await _contentfulManagementClient.GetCurrentUser();

        _logger.LogInformation("Logging into Contentful space {space} environment {environment}", _spaceId, _environmentId);

        _logger.LogInformation("Logged in as user {name} with id {id}", _user.Email, _user.SystemProperties.Id);

        _logger.LogInformation("Starting command {command}", context.Name);

        table.AddColumn(new TableColumn(new Text("Option", Globals.StyleSubHeading)).Alignment(Justify.Right));
        table.AddColumn(new TableColumn(new Text("", Globals.StyleSubHeading)));
        table.AddColumn(new TableColumn(new Text("Value", Globals.StyleSubHeading)));

        foreach (var prop in properties)
        {
            var attr = prop.GetAttributes<CommandOptionAttribute>().FirstOrDefault();

            if (attr != null)
            {
                var longName = attr.LongNames.FirstOrDefault();
                if (longName != null)
                {
                    var option = longName;
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