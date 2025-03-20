using Contentful.Core;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Exceptions;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Login;

public sealed class LoginCommand(IConsoleWriter console, IPersistedTokenCache tokenCache, AppSettings appSettings)
    : AsyncCommand<LoginCommand.Settings>
{
    private const string _contentfulPatPrefix = "CFPAT-";
    private const int _contentfulPatLength = 49;
    private const int _openAiPatLength = 10;

    private readonly IConsoleWriter _console = console;
    private readonly IPersistedTokenCache _tokenCache = tokenCache;
    private readonly AppSettings _appSettings = appSettings;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-a|--openai-endpoint <ENDPOINT>")]
        [Description("The Azure OpenAI endpoint.")]
        public string? OpenAiEndpoint { get; set; } = null!;

        [CommandOption("-k|--openai-token <TOKEN>")]
        [Description("The Azure OpenAI API key.")]
        public string? OpenAiApiKey { get; set; } = null!;

        [CommandOption("-n|--openai-deployment-name <NAME>")]
        [Description("The Azure OpenAI deployment name.")]
        public string? OpenAiDeploymentName { get; set; } = null!;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var currentSettings = _appSettings;

        _console.WriteNormal("You can create personal access tokens using the Contentful web app. To create a personal access token:");
        _console.WriteBlankLine();
        _console.WriteNormal("1. Log in to the Contentful web app.");
        _console.WriteNormal("2. Open the space that you want to access using the space selector in the top left.");
        _console.WriteNormal("3. Click Settings and select CMA tokens from the drop-down list.");
        _console.WriteNormal("4. Click Create personal access token. The Create personal access token window is displayed.");
        _console.WriteNormal("5. Enter a custom name for your personal access token and click Generate. Your personal access token is created.");
        _console.WriteNormal("6. Copy your personal access token to clipboard.");
        _console.WriteBlankLine();

        var contentfulApiKeyPrompt = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Contentful Management API Key:[/]")
            .PromptStyle(Globals.StyleAlertAccent)
            .DefaultValueStyle(Globals.StyleDim)
            .Secret()
            .DefaultValue(currentSettings?.ContentfulManagementApiKey ?? string.Empty)
            .Validate(ValidateContentfulApiKey);

        var contentfulManagementApiKey = settings.ManagementToken ?? _console.Prompt(contentfulApiKeyPrompt);

        string? spaceId = null;
        string? environmentId = null;

        if (settings.ManagementToken == null) _console.WriteBlankLine();

        using var httpClient = new HttpClient();
        var contentfulManagementClient = new ContentfulManagementClient(httpClient, contentfulManagementApiKey, string.Empty);

        var spaces = await contentfulManagementClient.GetSpaces();
        if (spaces is null || !spaces.Any())
        {
            throw new CliException("No spaces found.");
        }
        var promptSpace = new SelectionPrompt<string>()
            .Title($"[{Globals.StyleNormal.Foreground}]Select your default space:[/]")
            .PageSize(10)
            .MoreChoicesText($"[{Globals.StyleDim.ToMarkup()}](Move up and down to reveal more spaces)[/]")
            .HighlightStyle(Globals.StyleSubHeading);

        if (currentSettings?.ContentfulDefaultSpace == null || !spaces.Any(s => s.SystemProperties.Id.Equals(currentSettings.ContentfulDefaultSpace)))
        {
            promptSpace.AddChoices(spaces
                .OrderBy(e => e.Name)
                .Select(e => $"[{Globals.StyleDim.Foreground}]{e.Name}[/]"));
        }
        else
        {
            var currentSpaceName = spaces.First(s => s.SystemProperties.Id.Equals(currentSettings.ContentfulDefaultSpace)).Name;
            promptSpace.AddChoice($"[{Globals.StyleDim.Foreground}]{currentSpaceName}[/]");
            promptSpace.AddChoices(spaces
                .Where(e => !e.SystemProperties.Id.Equals(currentSettings.ContentfulDefaultSpace))
                .OrderBy(e => e.Name)
                .Select(e => $"[{Globals.StyleDim.Foreground}]{e.Name}[/]"));
        }

        promptSpace.DisabledStyle = Globals.StyleDim;

        var spaceName = settings.SpaceId ?? Markup.Remove(_console.Prompt(promptSpace));

        spaceId = spaces.First(s => s.Name.Equals(spaceName)).SystemProperties.Id;

        if (settings.SpaceId == null)
        {
            AnsiConsole.Markup($"[{Globals.StyleNormal.Foreground}]Select your default space:[/]");
            AnsiConsole.Markup($" [{Globals.StyleSubHeading.Foreground}]{spaceName}[/]");
            _console.WriteBlankLine();
            _console.WriteBlankLine();
        }

        var environments = await contentfulManagementClient.GetEnvironments(spaceId);

        if (environments is null || !environments.Any())
        {
            throw new CliException("No environments found.");
        }
        var promptEnvironment = new SelectionPrompt<string>()
            .Title($"[{Globals.StyleNormal.Foreground}]Select your default environment:[/]")
            .PageSize(10)
            .MoreChoicesText($"[{Globals.StyleDim.ToMarkup()}](Move up and down to reveal more environments)[/]")
            .HighlightStyle(Globals.StyleSubHeading);

        if (currentSettings?.ContentfulDefaultEnvironment == null)
        {
            promptEnvironment.AddChoices(environments
                .OrderBy(e => e.SystemProperties.Id)
                .Select(e => $"[{Globals.StyleDim.Foreground}]{e.SystemProperties.Id}[/]"));
        }
        else
        {
            promptEnvironment.AddChoice($"[{Globals.StyleDim.Foreground}]{currentSettings.ContentfulDefaultEnvironment}[/]");
            promptEnvironment.AddChoices(environments
                .Where(e => !e.SystemProperties.Id.Equals(currentSettings.ContentfulDefaultEnvironment))
                .OrderBy(e => e.SystemProperties.Id)
                .Select(e => $"[{Globals.StyleDim.Foreground}]{e.SystemProperties.Id}[/]"));
        }

        promptEnvironment.DisabledStyle = Globals.StyleDim;

        environmentId = settings.EnvironmentId ?? Markup.Remove(_console.Prompt(promptEnvironment));

        var contentfulDeliveryApiKeyPrompt = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Contentful Delivery API Key:[/]")
            .PromptStyle(Globals.StyleAlertAccent)
            .DefaultValueStyle(Globals.StyleDim)
            .Secret()
            .DefaultValue(currentSettings?.ContentfulDeliveryApiKey ?? string.Empty)
            .Validate(ValidateContentfulDeliveryAndPreviewApiKey);

        var contentfulDeliveryApiKey = settings.ContentDeliveryToken ?? _console.Prompt(contentfulDeliveryApiKeyPrompt);

        if (settings.ContentDeliveryToken == null) _console.WriteBlankLine();

        var contentfulPreviewApiKeyPrompt = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Contentful Preview API Key:[/]")
            .PromptStyle(Globals.StyleAlertAccent)
            .DefaultValueStyle(Globals.StyleDim)
            .Secret()
            .DefaultValue(currentSettings?.ContentfulPreviewApiKey ?? string.Empty)
            .Validate(ValidateContentfulDeliveryAndPreviewApiKey);

        var contentfulPreviewApiKey = settings.ContentPreviewToken ?? _console.Prompt(contentfulPreviewApiKeyPrompt);

        if (settings.ContentPreviewToken == null) _console.WriteBlankLine();

        var openAiApiKeyPrompt = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Open AI API Key:[/]")
                .PromptStyle(Globals.StyleAlertAccent)
                .DefaultValueStyle(Globals.StyleDim)
                .Secret()
                .DefaultValue(currentSettings?.OpenAiApiKey ?? string.Empty)
                .Validate(ValidateOpenAiApiKey);

        var openApiKey = settings.OpenAiApiKey ?? _console.Prompt(openAiApiKeyPrompt);

        if (settings.OpenAiApiKey == null) _console.WriteBlankLine();

        var openAiEndpointPrompt = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Open AI Endpoint:[/]")
            .PromptStyle(Globals.StyleAlertAccent)
            .DefaultValueStyle(Globals.StyleDim)
            .DefaultValue(currentSettings?.OpenAiEndpoint ?? string.Empty)
            .Validate(ValidateOpenAiEndpoint);

        var openApiEndpoint = settings.OpenAiEndpoint ?? _console.Prompt(openAiEndpointPrompt);

        if (settings.OpenAiEndpoint == null) _console.WriteBlankLine();

        var openAiDeploymentNamePrompt = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Open AI Deployment Name:[/]")
            .PromptStyle(Globals.StyleAlertAccent)
            .DefaultValueStyle(Globals.StyleDim)
            .DefaultValue(currentSettings?.OpenAiDeploymentName ?? string.Empty)
            .Validate(ValidateOpenAiDeploymentName);

        var openAiDeploymentName = settings.OpenAiDeploymentName ?? _console.Prompt(openAiDeploymentNamePrompt);

        if (settings.OpenAiDeploymentName == null) _console.WriteBlankLine();

        await _tokenCache.SaveAsync(Globals.AppName, new AppSettings()
        {
            ContentfulDefaultSpace = spaceId,
            ContentfulDefaultEnvironment = environmentId,
            ContentfulManagementApiKey = contentfulManagementApiKey,
            ContentfulDeliveryApiKey = contentfulDeliveryApiKey,
            ContentfulPreviewApiKey = contentfulPreviewApiKey,
            OpenAiApiKey = openApiKey,
            OpenAiEndpoint = openApiEndpoint,
            OpenAiDeploymentName = openAiDeploymentName,
        });

        return 0;
    }

    private ValidationResult ValidateContentfulApiKey(string pat)
    {
        if (pat.Length != _contentfulPatLength) return ValidationResult.Error("Invalid access token.");

        if (!pat.StartsWith(_contentfulPatPrefix)) return ValidationResult.Error("Invalid access token.");

        return ValidationResult.Success();
    }

    private ValidationResult ValidateContentfulDeliveryAndPreviewApiKey(string pat)
    {
        if (pat.Length < 40) return ValidationResult.Error("Invalid access token.");

        return ValidationResult.Success();
    }

    //TODO: Get back to this later. Check if length is no less than 10 characters for now.
    private ValidationResult ValidateOpenAiApiKey(string pat)
    {
        if (pat.Length < _openAiPatLength) return ValidationResult.Error("Invalid access token.");

        return ValidationResult.Success();
    }

    private ValidationResult ValidateOpenAiEndpoint(string uri)
    {
        var valid = Uri.TryCreate(uri, UriKind.Absolute, out _);

        if (!valid) return ValidationResult.Error("Invalid URI.");

        return ValidationResult.Success();
    }

    private ValidationResult ValidateOpenAiDeploymentName(string name)
    {
        if (name.Length < 3) return ValidationResult.Error("Invalid deployment name.");

        return ValidationResult.Success();
    }
}