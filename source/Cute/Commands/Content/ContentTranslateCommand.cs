using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
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

        var targetLocales = (await ContentfulConnection.GetLocalesAsync()).Where(k => k.Code != defaultLocale.Code).ToList();
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
                x => 
                {
                    if(Enum.TryParse<TranslationService>(x.TranslationService, out var translationService))
                    {
                        return translationService;
                    }
                    return TranslationService.Azure;
                }
            );

        if(!ConfirmWithPromptChallenge($"translate entries for {settings.ContentTypeId}"))
        {
            return -1;
        }

        Func<ITranslator, string, string, string, Task<string?>> translate = settings.UseCustomModel ?
            async (translator, text, from, to) =>
            {
                var translation = await translator.TranslateWithCustomModel(text, from, to);
                return translation?.Text;
            } :
            async (translator, text, from, to) =>
            {
                var translation = await translator.Translate(text, from, to);
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
                    var contentLocales = new ContentLocales(targetLocaleCodes, defaultLocale.Code);
                    var serializer = new EntrySerializer(contentType, contentLocales);

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
                                var targetLocaleFieldName = defaultLocaleFieldName.Replace($".{defaultLocale.Code}", $".{targetLocale.Code}");
                                if (!flatEntry.TryGetValue(targetLocaleFieldName, out var flatEntryTargetLocaleValue) || flatEntryTargetLocaleValue is null)
                                {
                                    symbols += defaultLocaleFieldValue.Length;
                                    TranslationService tService;
                                    if (!translationConfiguration.TryGetValue(targetLocale.Code, out tService))
                                    {
                                        tService = TranslationService.Azure;
                                    }
                                    var translator = _translateFactory.Create(tService);
                                    flatEntry[targetLocaleFieldName] = await translate(translator, defaultLocaleFieldValue, defaultLocale.Code, targetLocale.Code);
                                    entryChanged = true;
                                    taskTranslate.Description = $"{Emoji.Known.Robot} Translating ({symbols} symbols translated)";
                                }
                                taskTranslate.Increment(1);
                            }
                            
                        }

                        if (entryChanged)
                        {
                            var cloudEntry = await ContentfulConnection.GetManagementEntryAsync(entryId);
                            var deserializedEntry = serializer.DeserializeEntry(flatEntry);
                            cloudEntry.Fields = deserializedEntry.Fields;
                            needToPublish = true;
                            await ContentfulConnection.CreateOrUpdateEntryAsync(cloudEntry, entry.SystemProperties.Version);
                        }
                    }

                    taskTranslate.StopTask();
                });            

            var contentLocales = new ContentLocales(targetLocales.Select(t => t.Code).ToArray(), defaultLocale.Code);
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