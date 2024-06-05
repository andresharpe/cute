using Contentful.Core;
using Contentful.Core.Models;
using Cut.Constants;
using Cut.Lib.Contentful;
using Cut.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cut.Commands;

public class LoggedInCommand<TSettings> : AsyncCommand<TSettings> where TSettings : CommandSettings
{
    private readonly bool _isLoggedIn;

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

        var settings = _tokenCache.LoadAsync(Globals.AppName).Result;

        if (settings == null)
        {
            _isLoggedIn = false;
            return;
        }

        _isLoggedIn = true;

        _spaceId = settings.DefaultSpace;
        var apiKey = settings.ApiKey;

        _contentfulClient = new ContentfulManagementClient(_httpClient, apiKey, _spaceId);
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

        return Task.FromResult(0);
    }

    protected List<Entry<JObject>> GetContentfulEntries(string contentType, string sortOrder, ProgressTask progressTask)
    {
        List<Entry<JObject>> result = [];

        if (_contentfulClient is null) return result;

        progressTask.MaxValue = 1;

        foreach (var (entry, entries) in EntryEnumerator.Entries(_contentfulClient, contentType, sortOrder))
        {
            if (progressTask.MaxValue == 1)
            {
                progressTask.MaxValue = entries.Total;
            }

            result.Add(entry);

            progressTask.Increment(1);
        }
        return result;
    }
}