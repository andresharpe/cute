using ClosedXML;
using Contentful.Core;
using Contentful.Core.Configuration;
using Cute.Config;
using Cute.Constants;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using Table = Spectre.Console.Table;
using Text = Spectre.Console.Text;

namespace Cute.Commands;

public class LoggedInCommand<TSettings> : AsyncCommand<TSettings> where TSettings : CommandSettings
{
    private readonly bool _isLoggedIn;

    protected readonly IConsoleWriter _console;

    protected readonly IPersistedTokenCache _tokenCache;

    protected readonly ContentfulManagementClient? _contentfulManagementClient;

    protected readonly string _spaceId = string.Empty;

    private readonly HttpClient _httpClient;

    protected readonly AppSettings? _appSettings;

    public LoggedInCommand(IConsoleWriter console, IPersistedTokenCache tokenCache)
    {
        _console = console;
        _tokenCache = tokenCache;
        _httpClient = new HttpClient();

        _appSettings = _tokenCache.LoadAsync(Globals.AppName).Result;

        if (_appSettings == null)
        {
            _isLoggedIn = false;
            return;
        }

        _isLoggedIn = true;

        _spaceId = _appSettings.ContentfulDefaultSpace;

        var contentfulOptions = new ContentfulOptions()
        {
            ManagementApiKey = _appSettings.ContentfulManagementApiKey,
            SpaceId = _appSettings.ContentfulDefaultSpace,
            DeliveryApiKey = _appSettings.ContentfulDeliveryApiKey,
            PreviewApiKey = _appSettings.ContentfulPreviewApiKey,
            Environment = _appSettings.ContentfulDefaultEnvironment
        };
        _contentfulManagementClient = new ContentfulManagementClient(_httpClient, contentfulOptions);
    }

    public override Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
        if (!_isLoggedIn)
        {
            _console.WriteAlert("You are not authenticated to Contentful. To authenticate type:");
            _console.WriteBlankLine();
            _console.WriteAlertAccent("cut auth");
            return Task.FromResult(-1);
        }

        DisplaySettings(context, settings);

        return Task.FromResult(0);
    }

    private void DisplaySettings(CommandContext context, TSettings settings)
    {
        var settingsType = settings.GetType();

        var properties = settingsType.GetProperties();

        var table = new Table().NoBorder();

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
                    table.AddRow(
                        new Markup($"--{longName}", Globals.StyleDim),
                        new Markup($"=", Globals.StyleDim),
                        new Markup($"{prop.GetValue(settings)}", Globals.StyleNormal)
                    );
                }
            }
        }

        AnsiConsole.Write(new Markup("  " + string.Join(' ', context.Arguments), Globals.StyleAlertAccent));
        AnsiConsole.WriteLine();

        _console.WriteBlankLine();

        AnsiConsole.Write(table);

        _console.WriteBlankLine();
    }
}