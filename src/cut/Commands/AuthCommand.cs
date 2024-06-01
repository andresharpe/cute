using Contentful.Core;
using Contentful.Core.Errors;
using Cut.Config;
using Cut.Constants;
using Cut.Exceptions;
using Cut.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cut.Commands;

public class AuthCommand : AsyncCommand<AuthCommand.Settings>
{
    private const string patPrefix = "CFPAT-";
    private const int patLength = 49;
    private const int spaceIdMinLength = 10;

    private readonly IConsoleWriter _console;
    private readonly IPersistedTokenCache _tokenCache;

    public AuthCommand(IConsoleWriter console, IPersistedTokenCache tokenCache)
    {
        _console = console;
        _tokenCache = tokenCache;
    }

    public class Settings : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.WriteNormal("You can create personal access tokens using the Contentful web app. To create a personal access token:");
        _console.WriteBlankLine();
        _console.WriteNormal("1. Log in to the Contentful web app.");
        _console.WriteNormal("2. Open the space that you want to access using the space selector in the top left.");
        _console.WriteNormal("3. Click Settings and select CMA tokens from the drop-down list.");
        _console.WriteNormal("4. Click Create personal access token. The Create personal access token window is displayed.");
        _console.WriteNormal("5. Enter a custom name for your personal access token and click Generate. Your personal access token is created.");
        _console.WriteNormal("6. Copy your personal access token to clipboard.");
        _console.WriteBlankLine();

        var promptToken = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Contentful Management API Key:[/]")
            .PromptStyle(Globals.StyleAlertAccent)
            .Secret()
            .Validate(ValidatePat);

        var apiKey = _console.Prompt(promptToken);

        try
        {
            _console.WriteBlankLine();

            using var httpClient = new HttpClient();
            var contentfulClient = new ContentfulManagementClient(httpClient, apiKey, string.Empty);
            var spaces = await contentfulClient.GetSpaces();

            if (spaces is null || !spaces.Any())
            {
                throw new CliException("No spaces found.");
            }

            var promptSpace = new SelectionPrompt<string>()
                .Title($"[{Globals.StyleNormal.Foreground}]Select your default space:[/]")
                .PageSize(10)
                .MoreChoicesText($"[{Globals.StyleDim.ToMarkup()}](Move up and down to reveal more spaces)[/]")
                .HighlightStyle(Globals.StyleSubHeading)
                .AddChoices(spaces.Select(s => $"[{Globals.StyleDim.Foreground}]{s.Name}[/]"));

            promptSpace.DisabledStyle = Globals.StyleDim;

            var spaceName = Markup.Remove(_console.Prompt(promptSpace));

            var spaceId = spaces.First(s => s.Name.Equals(spaceName)).SystemProperties.Id;

            await _tokenCache.SaveAsync(Globals.AppName, new AppSettings()
            {
                ApiKey = apiKey,
                DefaultSpace = spaceId
            });
        }
        catch (ContentfulException ex)
        {
            throw new CliException(ex.Message, ex);
        }

        return 0;
    }

    private ValidationResult ValidatePat(string pat)
    {
        if (pat.Length != patLength) return ValidationResult.Error("Invalid access token.");

        if (!pat.StartsWith(patPrefix)) return ValidationResult.Error("Invalid access token.");

        return ValidationResult.Success();
    }

    private ValidationResult ValidateSpaceId(string spaceId)
    {
        if (spaceId.Length < spaceIdMinLength) return ValidationResult.Error("Invalid Contentful space identifier.");

        return ValidationResult.Success();
    }
}