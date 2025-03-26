﻿using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Contentful.CommandModels.TranslationGlossary;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.Serializers;
using Cute.Services;
using Cute.Services.Translation.Factories;
using Cute.Services.Translation.Interfaces;
using Cute.UiComponents;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Content;

public class ContentTranslateCommand(IConsoleWriter console, ILogger<ContentTranslateCommand> logger,
    AppSettings appSettings, TranslateFactory translateFactory, HttpClient httpClient) : BaseLoggedInCommand<ContentTranslateCommand.Settings>(console, logger, appSettings)
{
    private readonly TranslateFactory _translateFactory = translateFactory;
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : ContentCommandSettings
    {
        [CommandOption("-k|--key")]
        [Description("The key of the entry to translate.")]
        public string Key { get; set; } = default!;

        [CommandOption("-f|--field <CODE>")]
        [Description("List of fields to translate.")]
        public string[] Fields { get; set; } = default!;

        [CommandOption("--custom-model <CODE>")]
        [Description("Specifies whether custom model translation should be used")]
        public bool UseCustomModel { get; set; } = false;

        [CommandOption("--use-glossary <CODE>")]
        [Description("Specifies whether custom glossary (cuteTranslationGlossary) should be used")]
        public bool UseGlossary { get; set; } = false;
    }
    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var contentType = await GetContentTypeOrThrowError(settings.ContentTypeId);
        var defaultLocale = await ContentfulConnection.GetDefaultLocaleAsync();

        var fieldsToTranslate = contentType.Fields.Where(f => f.Localized).ToList();
        if(settings.Fields?.Length > 0)
        {
            fieldsToTranslate = fieldsToTranslate.Where(f => settings.Fields.Contains(f.Id)).ToList();
        }

        var contentLocales = await ContentfulConnection.GetLocalesAsync();
        var allContentLocales = new ContentLocales(contentLocales.Select(c => c.Code).ToArray(), defaultLocale.Code);

        var targetLocales = (contentLocales).Where(k => k.Code != defaultLocale.Code).ToList();
        if(settings.Locales?.Length > 0)
        {
            targetLocales = targetLocales.Where(k => settings.Locales.Contains(k.Code)).ToList();
        }

        var invalidFields = settings.Fields?.Except(fieldsToTranslate.Select(f => f.Id)).ToList();
        var invalidLocales = settings.Locales?.Except(targetLocales.Select(k => k.Code)).ToList();

        if(fieldsToTranslate.Count == 0)
        {
            _console.WriteException(new CliException($"No valid fields were provided to translate for content type {settings.ContentTypeId}"));
            return -1;
        }

        if(targetLocales.Count == 0)
        {
            _console.WriteException(new CliException("No valid locales provided to translate to"));
            return -1;
        }

        if(invalidFields?.Count > 0)
        {
            _console.WriteAlert($"Following fields do not exist: {string.Join(',', invalidFields.Select(f => $"'{f}'"))}");
        }

        var translationConfiguration = ContentfulConnection.GetPreviewEntries<CuteLanguage>()
            .ToBlockingEnumerable()
            .Select(x => x.Entry)
            .ToDictionary
            (
                x => x.Iso2Code,
                x => x
            );

        Dictionary<string, Dictionary<string, string>>? glossary = null;

        if (settings.UseGlossary)
        {
            var glossaryEnumerator = ContentfulConnection.GetManagementEntries<Entry<CuteTranslationGlossary>>("cuteTranslationGlossary");

            glossary = targetLocales.ToDictionary(x => x.Code, x => new Dictionary<string, string>());

            await foreach (var (entry, total) in glossaryEnumerator!)
            {
                if (!entry.Fields.Title.TryGetValue(defaultLocale.Code, out var key) || string.IsNullOrEmpty(key))
                {
                    continue;
                }

                foreach (var targetLocale in targetLocales)
                {
                    if (entry.Fields.Title.TryGetValue(targetLocale.Code, out var value) && !string.IsNullOrEmpty(value))
                    {
                        glossary[targetLocale.Code][key] = value;
                    }
                }
            }
        }

        var contentTypeTranslation = ContentfulConnection.GetPreviewEntryByKey<CuteContentTypeTranslation>(settings.ContentTypeId);

        if(!ConfirmWithPromptChallenge($"translate entries for {settings.ContentTypeId}"))
        {
            return -1;
        }

        Func<ITranslator, string, string, CuteLanguage, Task<string?>> translate = settings.UseCustomModel ?
            async (translator, text, from, to) =>
            {
                var translation = await translator.TranslateWithCustomModel(text, from, to, contentTypeTranslation, glossary?[to.Iso2Code]);
                return translation?.Text;
            } :
            async (translator, text, from, to) =>
            {
                var translation = await translator.Translate(text, from, to.Iso2Code, glossary?[to.Iso2Code]);
                return translation?.Text;
            };

        try
        {
            bool needToPublish = false;
            await ProgressBars.Instance()
                .AutoClear(false)
                .StartAsync(async ctx =>
                {
                    var taskTranslate = ctx.AddTask($"{Emoji.Known.Robot}  Translating (0 symbols translated)");

                    var targetLocaleCodes = targetLocales.Select(targetLocales => targetLocales.Code).ToArray();
                    var serializer = new EntrySerializer(contentType, allContentLocales);

                    var queryBuilder = new EntryQuery.Builder()
                    .WithContentType(settings.ContentTypeId)
                    .WithPageSize(1)
                    .WithLocale("*")
                    .WithIncludeLevels(0);

                    if (!string.IsNullOrEmpty(settings.Key))
                    {
                        queryBuilder.WithQueryString($"fields.key={settings.Key}");
                    }
                    
                    taskTranslate.MaxValue = 1;

                    long symbols = 0;

                    await foreach (var (entry, total) in ContentfulConnection.GetManagementEntries<Entry<JObject>>(
                        queryBuilder.Build()))
                    {
                        if (taskTranslate.MaxValue == 1)
                        {
                            taskTranslate.MaxValue = total * targetLocales.Count * fieldsToTranslate.Count;
                        }

                        var entryId = entry.SystemProperties.Id;
                        var entryChanged = false;
                        var flatEntry = serializer.SerializeEntry(entry);

                        var defaultLocaleFieldNames = flatEntry.Keys.Where(k => k.Contains($".{defaultLocale.Code}")).ToArray();

                        foreach (var defaultLocaleFieldName in defaultLocaleFieldNames)
                        {
                            if (!fieldsToTranslate.Any(f => defaultLocaleFieldName.StartsWith($"{f.Id}.")))
                            {
                                continue;
                            }

                            var flatEntryDefaultLocaleValue = flatEntry[defaultLocaleFieldName];
                            var defaultLocaleFieldValue = flatEntryDefaultLocaleValue?.ToString();

                            if (flatEntryDefaultLocaleValue is not string || string.IsNullOrEmpty(defaultLocaleFieldValue))
                            {
                                taskTranslate.Increment(targetLocales.Count);
                                continue;
                            }

                            foreach (var targetLocale in targetLocales)
                            {
                                if (!translationConfiguration.TryGetValue(targetLocale.Code, out var cuteLanguage))
                                {
                                    _console.WriteAlert($"No translation configuration found for locale {targetLocale.Code}");
                                    continue;
                                }

                                var targetLocaleFieldName = defaultLocaleFieldName.Replace($".{defaultLocale.Code}", $".{targetLocale.Code}");
                                if (!flatEntry.TryGetValue(targetLocaleFieldName, out var flatEntryTargetLocaleValue) || flatEntryTargetLocaleValue is null || string.IsNullOrEmpty(flatEntryTargetLocaleValue.ToString()))
                                {
                                    symbols += defaultLocaleFieldValue.Length;
                                    TranslationService tService;
                                    if(!Enum.TryParse(cuteLanguage.TranslationService, out tService))
                                    {
                                        tService = TranslationService.GPT4o;
                                    }
                                    var translator = _translateFactory.Create(tService);
                                    try
                                    {
                                        flatEntry[targetLocaleFieldName] = await translate(translator, defaultLocaleFieldValue, defaultLocale.Code, cuteLanguage);
                                        entryChanged = true;
                                        taskTranslate.Description = $"{Emoji.Known.Robot} Translating ({symbols} symbols translated)";
                                    }
                                    catch (Exception ex)
                                    {
                                        _console.WriteAlert($"Error translating text from {defaultLocale.Code} to {targetLocale.Code} using {tService}. Error Message: {ex.Message}");
                                    }
                                }
                                taskTranslate.Increment(1);
                            }
                            
                        }

                        if (entryChanged)
                        {
                            var cloudEntry = await ContentfulConnection.GetManagementEntryAsync(entryId);
                            var deserializedEntry = serializer.DeserializeEntry(flatEntry);

                            foreach (var field in fieldsToTranslate)
                            {
                                foreach (var localeCode in targetLocaleCodes)
                                {
                                    cloudEntry.Fields[field.Id][localeCode] = deserializedEntry.Fields[field.Id]![localeCode];
                                }
                            }

                            needToPublish = true;
                            await ContentfulConnection.CreateOrUpdateEntryAsync(cloudEntry, entry.SystemProperties.Version);
                        }
                    }

                    taskTranslate.StopTask();
                });            

            if (needToPublish)
            {
                await PerformBulkOperations(
                    [
                        new PublishBulkAction(ContentfulConnection, _httpClient)
                            .WithContentType(contentType)
                            .WithContentLocales(await ContentfulConnection.GetContentLocalesAsync())
                            .WithVerbosity(settings.Verbosity),
                    ]
                );
            }
            else
            {
                Console.WriteLine("There are no entries to translate.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _console.WriteException(ex);
            return 1;
        }
    }
}