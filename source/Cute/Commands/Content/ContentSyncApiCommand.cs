﻿using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Cache;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Contentful.CommandModels.ContentSyncApi;
using Cute.Lib.Exceptions;
using Cute.Lib.InputAdapters.Http;
using Cute.Lib.InputAdapters.Http.Models;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cute.Commands.Content;

public class ContentSyncApiCommand(IConsoleWriter console, ILogger<ContentSyncApiCommand> logger,
    ContentfulConnection contentfulConnection, AppSettings appSettings, HttpClient httpClient,
    HttpResponseFileCache httpResponseCache)
    : BaseLoggedInCommand<ContentSyncApiCommand.Settings>(console, logger, contentfulConnection, appSettings)
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
        var contentMetaType = CuteContentSyncApiContentType.Instance();

        if (await CreateContentTypeIfNotExist(contentMetaType))
        {
            _console.WriteNormalWithHighlights($"Created content type {contentMetaType.SystemProperties.Id}...", Globals.StyleHeading);
        }

        var contentMetaTypeId = contentMetaType.SystemProperties.Id;

        var contentLocales = new ContentLocales([DefaultLocaleCode], DefaultLocaleCode);

        var apiSyncEntry = CuteContentSyncApi.GetByKey(ContentfulClient, settings.Key)
            ?? throw new CliException($"No API sync entry '{contentMetaTypeId}' with key '{settings.Key}' was found.");

        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var adapter = yamlDeserializer.Deserialize<HttpDataAdapterConfig>(apiSyncEntry.Yaml)
            ?? throw new CliException($"Invalid data in '{contentMetaTypeId}.{"yaml"}' for key '{settings.Key}'.");

        adapter.Id = settings.Key;

        var contentType = GetContentTypeOrThrowError(adapter.ContentType, $"Syncing '{contentMetaTypeId}' entry with key '{settings.Key}'.");

        await PerformBulkOperations([

            new UpsertBulkAction(_contentfulConnection, _httpClient)
                .WithContentType(contentType)
                .WithContentLocales(contentLocales)
                .WithNewEntries(
                    new HttpInputAdapter(
                        adapter,
                        _contentfulConnection,
                        contentLocales,
                        _appSettings.GetSettings(),
                        ContentTypes,
                        _httpClient
                    )
                    .WithHttpResponseFileCache(settings.UseFileCache ?  _httpResponseCache : null )
                )
                .WithMatchField(adapter.ContentKeyField)
                .WithApplyChanges(settings.Apply)
                .WithVerbosity(settings.Verbosity),

            new PublishBulkAction(_contentfulConnection, _httpClient)
                .WithContentType(contentType)
                .WithContentLocales(contentLocales)
                .WithVerbosity(settings.Verbosity)
        ]);

        return 0;
    }
}