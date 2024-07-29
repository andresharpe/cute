using ClosedXML;
using Contentful.Core;
using Contentful.Core.Configuration;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using Table = Spectre.Console.Table;
using Text = Spectre.Console.Text;

namespace Cute.Commands;

public abstract class LoggedInCommand<TSettings> : AsyncCommand<TSettings> where TSettings : CommandSettings
{
    protected readonly IConsoleWriter _console;

    private readonly ILogger _logger;

    private readonly ContentfulConnection _contentfulConnection;

    private User _contentfulUser = new();

    private Space _contentfulSpace = new();

    private List<Locale> _contentfulLocales = [];

    private Locale _defaultLocale = new();

    private string _defaultLocaleCode = "en";

    protected ContentfulManagementClient ContentfulManagementClient => _contentfulConnection.ManagementClient;
    protected ContentfulClient ContentfulClient => _contentfulConnection.DeliveryClient;
    protected ContentfulOptions ContentfulOptions => _contentfulConnection.Options;
    protected string ContentfulSpaceId => ContentfulOptions.SpaceId;
    protected string ContentfulEnvironmentId => ContentfulOptions.Environment;
    protected User ContentfulUser => _contentfulUser;
    protected Space ContentfulSpace => _contentfulSpace;
    protected IEnumerable<Locale> Locales => _contentfulLocales;
    protected Locale DefaultLocale => _defaultLocale;
    protected string DefaultLocaleCode => _defaultLocaleCode;

    protected readonly AppSettings _appSettings;

    public LoggedInCommand(IConsoleWriter console, ILogger logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings)
    {
        _console = console;

        _console.Logger = logger;

        _logger = logger;

        _contentfulConnection = contentfulConnection;

        _appSettings = appSettings;
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

        var showTable = false;

        _contentfulUser = await ContentfulManagementClient.GetCurrentUser();

        _contentfulSpace = await ContentfulManagementClient.GetSpace(ContentfulSpaceId);

        _contentfulLocales = [.. (await ContentfulManagementClient.GetLocalesCollection())];

        _defaultLocale = _contentfulLocales.First(l => l.Default);

        _defaultLocaleCode = _defaultLocale.Code;

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
                    showTable = true;
                }
            }
        }

        _console.Write(new Markup("  " + string.Join(' ', context.Arguments), Globals.StyleAlertAccent));

        _console.WriteLine();

        _console.WriteBlankLine();

        if (showTable)
        {
            _console.Write(table);

            _console.WriteBlankLine();
        }
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
}