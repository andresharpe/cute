using Contentful.Core;
using Contentful.Core.Errors;
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
        var promptToken = new TextPrompt<string>("Enter your Contentful Management API Key:")
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

            if (spaces is null || spaces.Count() == 0)
            {
                throw new CliException("No spaces found.");
            }

            var promptSpace = new SelectionPrompt<string>()
                .Title("Select your default space:")
                .PageSize(10)
                .MoreChoicesText($"[{Globals.StyleDim.ToMarkup()}](Move up and down to reveal more spaces)[/]")
                .AddChoices(spaces.Select(s => s.Name));

            var spaceName = _console.Prompt(promptSpace);

            var spaceId = spaces.First(s => s.Name.Equals(spaceName)).SystemProperties.Id;

            await _tokenCache.SaveAsync(Globals.AppName, apiKey + "|" + spaceId);
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