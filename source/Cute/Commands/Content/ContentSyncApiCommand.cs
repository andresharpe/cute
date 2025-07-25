using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Cache;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Contentful.CommandModels.ContentSyncApi;
using Cute.Lib.Exceptions;
using Cute.Lib.InputAdapters;
using Cute.Lib.InputAdapters.Base.Models;
using Cute.Lib.InputAdapters.Http;
using Cute.Lib.InputAdapters.Http.Models;
using Cute.Lib.InputAdapters.DB;
using Cute.Lib.InputAdapters.DB.Model;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cute.Commands.Content;

public class ContentSyncApiCommand(IConsoleWriter console, ILogger<ContentSyncApiCommand> logger,
    AppSettings appSettings, HttpClient httpClient,
    HttpResponseFileCache httpResponseCache)
    : BaseLoggedInCommand<ContentSyncApiCommand.Settings>(console, logger, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly HttpResponseFileCache _httpResponseCache = httpResponseCache;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-k|--key")]
        [Description("The key of the cuteContentSyncApi entry.")]
        public string Key { get; set; } = default!;

        [CommandOption("-a|--apply")]
        [Description("Apply and publish all the required edits.")]
        public bool Apply { get; set; } = false;

        [CommandOption("-u|--use-filecache")]
        [Description("Whether or not to cache responses to a local file cache for subsequent calls.")]
        public bool UseFileCache { get; set; } = false;

        [CommandOption("--no-publish")]
        [Description("Specifies whether to skip publish for modified entries")]
        public bool NoPublish { get; set; } = false;

        [CommandOption("--use-session")]
        [Description("Indicates whether to use session (eg: publish only entries modified by the command and not all the unpublished ones).")]
        public bool UseSession { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.Key))
        {
            return ValidationResult.Error("The key of the of the 'cuteContentSyncApi' is required. Specify it with '-k' or '--key'.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        IInputAdapter inputAdapter;

        var contentSyncApiType = CuteContentSyncApiContentType.Instance();

        if (await CreateContentTypeIfNotExist(contentSyncApiType))
        {
            _console.WriteNormalWithHighlights($"Created content type {contentSyncApiType.SystemProperties.Id}...", Globals.StyleHeading);
        }

        var contentSyncApiTypeId = contentSyncApiType.SystemProperties.Id;

        var defaultLocale = await ContentfulConnection.GetDefaultLocaleAsync();       

        var contentLocales = new ContentLocales((await ContentfulConnection.GetContentLocalesAsync()).Locales, defaultLocale.Code);

        var apiSyncEntry = ContentfulConnection.GetPreviewEntryByKey<CuteContentSyncApi>(settings.Key)
            ?? throw new CliException($"No API sync entry '{contentSyncApiTypeId}' with key '{settings.Key}' was found.");

        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        DataAdapterConfigBase adapter;

        switch (apiSyncEntry.SourceType?.ToLowerInvariant())
        {
            case "database":
                adapter = yamlDeserializer.Deserialize<DBDataAdapterConfig>(apiSyncEntry.Yaml)
                    ?? throw new CliException($"Invalid data in '{contentSyncApiTypeId}.{"yaml"}' for key '{settings.Key}'.");

                inputAdapter = new DBInputAdapter(
                                (DBDataAdapterConfig)adapter,
                                ContentfulConnection,
                                contentLocales,
                                AppSettings.GetSettings(),
                                await ContentfulConnection.GetContentTypesAsync()
                            );
                break;
            case "restapi":
            default:
                adapter = yamlDeserializer.Deserialize<HttpDataAdapterConfig>(apiSyncEntry.Yaml)
                    ?? throw new CliException($"Invalid data in '{contentSyncApiTypeId}.{"yaml"}' for key '{settings.Key}'.");

                inputAdapter = new HttpInputAdapter(
                                (HttpDataAdapterConfig)adapter,
                                ContentfulConnection,
                                contentLocales,
                                AppSettings.GetSettings(),
                                await ContentfulConnection.GetContentTypesAsync(),
                                _httpClient
                            )
                            .WithHttpResponseFileCache(settings.UseFileCache ? _httpResponseCache : null);
                break;
        }

        adapter.Id = settings.Key;

        var contentType = await GetContentTypeOrThrowError(adapter.ContentType, $"Syncing '{contentSyncApiTypeId}' entry with key '{settings.Key}'.");

        if (!ConfirmWithPromptChallenge($"sync content for '{contentType.SystemProperties.Id}'"))
        {
            return -1;
        }

        await PerformBulkOperations([
            new UpsertBulkAction(ContentfulConnection, _httpClient, true)
                .WithContentType(contentType)
                .WithContentLocales(contentLocales)
                .WithNewEntries(inputAdapter)
                .WithMatchField(adapter.ContentKeyField)
                .WithApplyChanges(settings.Apply)
                .WithVerbosity(settings.Verbosity),
            new PublishBulkAction(ContentfulConnection, _httpClient)
            .WithContentType(contentType)
            .WithContentLocales(contentLocales)
            .WithVerbosity(settings.Verbosity)
            .WithApplyChanges(!settings.NoPublish)
            .WithUseSession(settings.UseSession)
        ], apiSyncEntry.Key);

        return 0;
    }
}