using Contentful.Core;
using Contentful.Core.Errors;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Exceptions;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cute.Commands;

public class AuthCommand : AsyncCommand<AuthCommand.Settings>
{
    private const string _contentfulPatPrefix = "CFPAT-";
    private const int _contentfulPatLength = 49;
    private const int _openAiPatLength = 32;

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
        var currentSettings = await _tokenCache.LoadAsync(Globals.AppName);

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

        var contentfulApiKey = _console.Prompt(contentfulApiKeyPrompt);

        string? spaceId = null;
        string? environmentId = null;

        try
        {
            _console.WriteBlankLine();

            using var httpClient = new HttpClient();
            var contentfulClient = new ContentfulManagementClient(httpClient, contentfulApiKey, string.Empty);

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

            spaceId = spaces.First(s => s.Name.Equals(spaceName)).SystemProperties.Id;

            var environments = await contentfulClient.GetEnvironments(spaceId);
            if (environments is null || !environments.Any())
            {
                throw new CliException("No environments found.");
            }
            var promptEnvironment = new SelectionPrompt<string>()
                .Title($"[{Globals.StyleNormal.Foreground}]Select your default environment:[/]")
                .PageSize(10)
                .MoreChoicesText($"[{Globals.StyleDim.ToMarkup()}](Move up and down to reveal more environments)[/]")
                .HighlightStyle(Globals.StyleSubHeading)
                .AddChoices(environments.Select(e => $"[{Globals.StyleDim.Foreground}]{e.SystemProperties.Id}[/]"));

            promptEnvironment.DisabledStyle = Globals.StyleDim;

            environmentId = Markup.Remove(_console.Prompt(promptEnvironment));
        }
        catch (ContentfulException ex)
        {
            throw new CliException(ex.Message, ex);
        }

        var contentfulDeliveryApiKeyPrompt = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Contentful Delivery API Key:[/]")
            .PromptStyle(Globals.StyleAlertAccent)
            .DefaultValueStyle(Globals.StyleDim)
            .Secret()
            .DefaultValue(currentSettings?.ContentfulDeliveryApiKey ?? string.Empty)
            .Validate(ValidateContentfulDeliveryAndPreviewApiKey);

        var contentfulDeliveryApiKey = _console.Prompt(contentfulDeliveryApiKeyPrompt);

        _console.WriteBlankLine();

        var contentfulPreviewApiKeyPrompt = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Contentful Preview API Key:[/]")
            .PromptStyle(Globals.StyleAlertAccent)
            .DefaultValueStyle(Globals.StyleDim)
            .Secret()
            .DefaultValue(currentSettings?.ContentfulPreviewApiKey ?? string.Empty)
            .Validate(ValidateContentfulDeliveryAndPreviewApiKey);

        var contentfulPreviewApiKey = _console.Prompt(contentfulPreviewApiKeyPrompt);

        _console.WriteBlankLine();

        var openAiApiKeyPrompt = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Open AI API Key:[/]")
                .PromptStyle(Globals.StyleAlertAccent)
                .DefaultValueStyle(Globals.StyleDim)
                .Secret()
                .DefaultValue(currentSettings?.OpenAiApiKey ?? string.Empty)
                .Validate(ValidateOpenAiApiKey);

        var openApiKey = _console.Prompt(openAiApiKeyPrompt);

        _console.WriteBlankLine();

        var openAiEndpointPrompt = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Open AI Endpoint:[/]")
            .PromptStyle(Globals.StyleAlertAccent)
            .DefaultValueStyle(Globals.StyleDim)
            .DefaultValue(currentSettings?.OpenAiEndpoint ?? string.Empty)
            .Validate(ValidateOpenAiEndpoint);

        var openApiEndpoint = _console.Prompt(openAiEndpointPrompt);

        _console.WriteBlankLine();

        var openAiDeploymentNamePrompt = new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]Enter your Open AI Deployment Name:[/]")
            .PromptStyle(Globals.StyleAlertAccent)
            .DefaultValueStyle(Globals.StyleDim)
            .DefaultValue(currentSettings?.OpenAiDeploymentName ?? string.Empty)
            .Validate(ValidateOpenAiDeploymentName);

        var openAiDeploymentName = _console.Prompt(openAiDeploymentNamePrompt);

        await _tokenCache.SaveAsync(Globals.AppName, new AppSettings()
        {
            ContentfulDefaultSpace = spaceId,
            ContentfulDefaultEnvironment = environmentId,
            ContentfulManagementApiKey = contentfulApiKey,
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

    private ValidationResult ValidateOpenAiApiKey(string pat)
    {
        if (pat.Length != _openAiPatLength) return ValidationResult.Error("Invalid access token.");

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