using Contentful.Core;
using Cut.Constants;
using Cut.Services;
using Spectre.Console.Cli;

namespace Cut.Commands;

public class LoggedInCommand<TSettings> : AsyncCommand<TSettings> where TSettings : CommandSettings
{
    private readonly bool _isLoggedIn;

    private readonly bool _isCacheValid;

    protected readonly IConsoleWriter _console;

    protected readonly IPersistedTokenCache _tokenCache;

    protected readonly ContentfulManagementClient? _contentfulClient;

    protected readonly string _spaceId = string.Empty;

    private readonly HttpClient _httpClient;

    public LoggedInCommand(IConsoleWriter console, IPersistedTokenCache tokenCache)
    {
        _console = console;
        _tokenCache = tokenCache;
        _httpClient = new HttpClient();

        var token = _tokenCache.LoadAsync(Globals.AppName).Result;

        if (token == null)
        {
            _isLoggedIn = false;
            return;
        }

        _isLoggedIn = true;

        var components = token.Split('|');

        if (components.Length != 2)
        {
            _isCacheValid = false;
            return;
        }

        _spaceId = components[1];
        var apiKey = components[0];

        _isCacheValid = true;

        _contentfulClient = new ContentfulManagementClient(_httpClient, apiKey, _spaceId);
    }

    public override Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
        if (!_isLoggedIn)
        {
            _console.WriteAlert("You are not authenticated to Contentful. To authenticate type:");
            _console.WriteBlankLine();
            _console.WriteAlertAccent("cut login");
            return Task.FromResult(-1);
        }

        if (!_isCacheValid)
        {
            _console.WriteAlert("The credential cache is corrupt. To re-authenticate type:");
            _console.WriteBlankLine();
            _console.WriteAlertAccent("cut login");
            return Task.FromResult(-1);
        }

        return Task.FromResult(0);
    }
}