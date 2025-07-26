using Cute.Commands.BaseCommands;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Exceptions;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Content;

public class ContentClearLocalizationCommand(IConsoleWriter console, ILogger<ContentTranslateCommand> logger,
    AppSettings appSettings, HttpClient httpClient) : BaseLoggedInCommand<ContentClearLocalizationCommand.Settings>(console, logger, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : ContentCommandSettings
    {
        [CommandOption("-k|--key")]
        [Description("The key of the entry to clear.")]
        public string Key { get; set; } = default!;

        [CommandOption("-f|--field <CODE>")]
        [Description("List of fields to clear.")]
        public string[] Fields { get; set; } = default!;

        [CommandOption("-a|--apply")]
        [Description("Apply and publish all the required edits. The default behaviour is to only list the detected changes.")]
        public bool Apply { get; set; } = false;
    }
    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var contentType = await GetContentTypeOrThrowError(settings.ContentTypeId);
        var defaultLocale = await ContentfulConnection.GetDefaultLocaleAsync();

        var fieldsToTranslate = contentType.Fields.Where(f => f.Localized).ToList();
        if (settings.Fields?.Length > 0)
        {
            fieldsToTranslate = fieldsToTranslate.Where(f => settings.Fields.Contains(f.Id)).ToList();
        }

        var targetLocales = (await ContentfulConnection.GetLocalesAsync()).Where(k => k.Code != defaultLocale.Code).ToList();
        if (settings.Locales is null || settings.Locales.Length == 0)
        {
            _console.WriteException(new CliException("No valid locales provided to clear"));
            return -1;
        }

        targetLocales = targetLocales.Where(k => settings.Locales!.Contains(k.Code)).ToList();

        var invalidFields = settings.Fields?.Except(fieldsToTranslate.Select(f => f.Id)).ToList();
        var invalidLocales = settings.Locales?.Except(targetLocales.Select(k => k.Code)).ToList();

        if (fieldsToTranslate.Count == 0)
        {
            _console.WriteException(new CliException($"No valid fields were provided to clear for content type {settings.ContentTypeId}"));
            return -1;
        }

        if (targetLocales.Count == 0)
        {
            _console.WriteException(new CliException("No valid locales provided to clear"));
            return -1;
        }

        if (invalidFields?.Count > 0)
        {
            _console.WriteAlert($"Following fields do not exist: {string.Join(',', invalidFields.Select(f => $"'{f}'"))}");
        }

        if (!ConfirmWithPromptChallenge($"clear field(s) for {settings.ContentTypeId} entries"))
        {
            return -1;
        }

        var contentLocales = new ContentLocales(targetLocales.Select(locale => locale.Code).ToArray(), defaultLocale.Code);

        await PerformBulkOperations([
            new ClearFieldsBulkAction(ContentfulConnection, _httpClient, fieldsToTranslate.Select(f => f.Name).ToList(), settings.Key)
                .WithContentType(contentType)
                .WithContentLocales(contentLocales)
                .WithVerbosity(settings.Verbosity)
                .WithApplyChanges(settings.Apply),
            new PublishBulkAction(ContentfulConnection, _httpClient)
                .WithContentType(contentType)
                .WithContentLocales(contentLocales)
                .WithVerbosity(settings.Verbosity)
                .WithApplyChanges(!settings.NoPublish)
                .WithUseSession(settings.UseSession)
        ]);

        return 0;
    }
}