using Contentful.Core.Models;
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
using System.Collections.Concurrent;

namespace Cute.Commands.Content;

public class ContentTranslateCommand(IConsoleWriter console, ILogger<ContentTranslateCommand> logger,
    AppSettings appSettings, TranslateFactory translateFactory, HttpClient httpClient) : BaseLoggedInCommand<ContentTranslateCommand.Settings>(console, logger, appSettings)
{
    private const int ENTRY_BATCH_SIZE = 20;
    private const int TRANSLATION_BATCH_SIZE = 50;
    private const int MAX_ENTRY_UPDATE_CONCURRENCY = 10;
    
    private readonly TranslateFactory _translateFactory = translateFactory;
    private readonly HttpClient _httpClient = httpClient;
    private readonly ConcurrentDictionary<TranslationService, ITranslator> _translatorCache = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _translationCache = new();
    private readonly List<(string entryId, Entry<dynamic> entry, int version)> _pendingUpdates = new();
    private Func<ITranslator, string, string, CuteLanguage, Task<string?>> translate = default!;

    public class Settings : ContentCommandSettings
    {
        [CommandOption("-f|--field <CODE>")]
        [Description("List of fields to translate.")]
        public string[] Fields { get; set; } = default!;

        [CommandOption("--custom-model <CODE>")]
        [Description("Specifies whether custom model translation should be used")]
        public bool UseCustomModel { get; set; } = false;

        [CommandOption("--use-glossary <CODE>")]
        [Description("Specifies whether custom glossary (cuteTranslationGlossary) should be used")]
        public bool UseGlossary { get; set; } = false;

        [CommandOption("--filter-field")]
        [Description("The field to update.")]
        public string FilterField { get; set; } = null!;

        [CommandOption("--filter-field-value")]
        [Description("The value to update it with. Can contain an expression.")]
        public string FilterFieldValue { get; set; } = null!;

        [CommandOption("--max-concurrency")]
        [Description("Indicates how many concurrent calls can be made to a translation service for a single entry. Default is 10")]
        public int MaxConcurrency { get; set; } = 10;

        [CommandOption("--fallback-service")]
        [Description("Fallback translation service (Azure, Google, Deepl, GPT4o), in case configured one doesn't return a value. Will translate without a custom model and glossary")]
        public TranslationService? FallbackService { get; set; } = null;
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

        string queryString = string.Empty;
        if (!string.IsNullOrEmpty(settings.FilterField))
        {
            if (string.IsNullOrEmpty(settings.FilterFieldValue))
            {
                throw new CliException($"The filter field value is required when using the filter field.");
            }
            else
            {
                queryString = $"fields.{settings.FilterField}={settings.FilterFieldValue}";
            }
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

        translate = settings.UseCustomModel ?
        async (translator, text, from, to) =>
        {
            try
            {
                var translation = await translator.TranslateWithCustomModel(text, from, to, contentTypeTranslation, glossary?[to.Iso2Code]);
                return translation?.Text;
            }
            catch (Exception ex)
            {
                _console.WriteAlert($"Error translating text from {from} to {to} using {translator.GetType().Name}. Error Message: {ex.Message}");
                return null;
            }
        }
        :
        async (translator, text, from, to) =>
        {
            try
            {
                var translation = await translator.Translate(text, from, to.Iso2Code, glossary?[to.Iso2Code]);
                return translation?.Text;
            }
            catch (Exception ex)
            {
                _console.WriteAlert($"Error translating text from {from} to {to} using {translator.GetType().Name}. Error Message: {ex.Message}");
                return null;
            }
        };

        try
        {
            // Create semaphores to limit concurrent operations
            var throttler = new SemaphoreSlim(settings.MaxConcurrency);
            var updateSemaphore = new SemaphoreSlim(MAX_ENTRY_UPDATE_CONCURRENCY);
            bool needToPublish = false;
            Dictionary<string, List<string>> failedEntryIds = new Dictionary<string, List<string>>();
            
            await ProgressBars.Instance()
                .AutoClear(false)
                .StartAsync(async ctx =>
                {
                    var taskTranslate = ctx.AddTask($"{Emoji.Known.Robot}  Translating (0 symbols translated)");

                    var targetLocaleCodes = targetLocales.Select(tl => tl.Code).ToArray();
                    var serializer = new EntrySerializer(contentType, allContentLocales);
                    var fieldMappings = BuildFieldMappings(defaultLocale.Code, targetLocales, fieldsToTranslate);

                    var queryBuilder = new EntryQuery.Builder()
                        .WithContentType(settings.ContentTypeId)
                        .WithPageSize(100)
                        .WithLocale("*")
                        .WithIncludeLevels(0);

                    if (!string.IsNullOrEmpty(queryString))
                    {
                        queryBuilder.WithQueryString(queryString);
                    }
                    
                    taskTranslate.MaxValue = 1;
                    long symbols = 0;
                    
                    var entryBatch = new List<Entry<JObject>>();
                    int totalEntries = 0;

                    await foreach (var (entry, total) in ContentfulConnection.GetManagementEntries<Entry<JObject>>(
                        queryBuilder.Build()))
                    {
                        if (taskTranslate.MaxValue == 1)
                        {
                            taskTranslate.MaxValue = total * targetLocales.Count * fieldsToTranslate.Count;
                            totalEntries = total;
                        }

                        entryBatch.Add(entry);
                        
                        if (entryBatch.Count >= ENTRY_BATCH_SIZE)
                        {
                            var (batchSymbols, batchNeedsPublish, batchFailures) = await ProcessEntryBatch(
                                entryBatch, 
                                serializer, 
                                fieldMappings, 
                                fieldsToTranslate, 
                                targetLocales, 
                                targetLocaleCodes, 
                                defaultLocale, 
                                translationConfiguration, 
                                settings, 
                                throttler,
                                updateSemaphore,
                                taskTranslate);
                            
                            symbols += batchSymbols;
                            needToPublish = needToPublish || batchNeedsPublish;
                            
                            foreach (var failure in batchFailures)
                            {
                                if (!failedEntryIds.ContainsKey(failure.Key))
                                    failedEntryIds[failure.Key] = new List<string>();
                                failedEntryIds[failure.Key].AddRange(failure.Value);
                            }
                            
                            taskTranslate.Description = $"{Emoji.Known.Robot} Translating ({symbols} symbols translated)";
                            entryBatch.Clear();
                        }
                    }

                    // Process remaining entries in the batch
                    if (entryBatch.Count > 0)
                    {
                        var (batchSymbols, batchNeedsPublish, batchFailures) = await ProcessEntryBatch(
                            entryBatch, 
                            serializer, 
                            fieldMappings, 
                            fieldsToTranslate, 
                            targetLocales, 
                            targetLocaleCodes, 
                            defaultLocale, 
                            translationConfiguration, 
                            settings, 
                            throttler,
                            updateSemaphore,
                            taskTranslate);
                        
                        symbols += batchSymbols;
                        needToPublish = needToPublish || batchNeedsPublish;
                        
                        foreach (var failure in batchFailures)
                        {
                            if (!failedEntryIds.ContainsKey(failure.Key))
                                failedEntryIds[failure.Key] = new List<string>();
                            failedEntryIds[failure.Key].AddRange(failure.Value);
                        }
                    }

                    // Flush any remaining pending updates
                    await FlushPendingUpdates(updateSemaphore);
                    
                    taskTranslate.Description = $"{Emoji.Known.Robot} Translation completed ({symbols} symbols translated)";
                    taskTranslate.StopTask();
                });

            if (!needToPublish)
            {
                Console.WriteLine("There are no entries to translate.");
                return 0;
            }

            await PerformBulkOperations(
                [
                    new PublishBulkAction(ContentfulConnection, _httpClient)
                        .WithContentType(contentType)
                        .WithContentLocales(await ContentfulConnection.GetContentLocalesAsync())
                        .WithVerbosity(settings.Verbosity)
                        .WithApplyChanges(!settings.NoPublish),
                ]
            );

            if(failedEntryIds.Count > 0)
            {
                throw new CliException($"Failed to translate following entries:\n{string.Join("\n", failedEntryIds.Select(x => $"{x.Key} ({string.Join(", ", x.Value)})"))}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _console.WriteException(ex);
            return 1;
        }

    }

    private async Task<(string targetField, string? translatedText, bool success)> TranslateFieldAsync(
    string text,
    string sourceLocale,
    CuteLanguage targetLanguage,
    string targetField,
    TranslationService tService,
    TranslationService? fallbackService,
    SemaphoreSlim throttler)
    {
        await throttler.WaitAsync(); // Wait for a slot to be available

        try
        {
            var translator = _translateFactory.Create(tService);
            string? translatedText = null;
            int retryCount = 3;

            while (retryCount > 0)
            {
                try
                {
                    translatedText = await translate(translator, text, sourceLocale, targetLanguage);
                    if (!string.IsNullOrEmpty(translatedText))
                        break;
                }
                catch (Exception ex)
                {
                    if (retryCount == 1) // Only log on final attempt
                        _console.WriteAlert($"Error translating text: {ex.Message}");
                }

                retryCount--;
                if (retryCount > 0)
                    await Task.Delay(1000); // Wait before retry
            }

            if(string.IsNullOrEmpty(translatedText) && fallbackService != null && tService != fallbackService)
            {
                translator = _translateFactory.Create(fallbackService.Value);
                try
                {
                    var translation = await translator.Translate(text, sourceLocale, targetLanguage.Iso2Code);
                    translatedText = translation?.Text;
                }
                catch (Exception ex)
                {
                    _console.WriteAlert($"Error translating text from {sourceLocale} to {targetLanguage.Iso2Code} using {translator.GetType().Name}. Error Message: {ex.Message}");
                    translatedText = null;
                }
            }

            return (targetField, translatedText, !string.IsNullOrEmpty(translatedText));
        }
        catch (Exception ex)
        {
            _console.WriteAlert($"Error translating text from {sourceLocale} to {targetLanguage.Iso2Code}. Error: {ex.Message}");
            return (targetField, null, false);
        }
        finally
        {
            throttler.Release(); // Always release the throttler
        }
    }

    private async Task<(long symbols, bool needsPublish, Dictionary<string, List<string>> failures)> ProcessEntryBatch(
        List<Entry<JObject>> entries,
        EntrySerializer serializer,
        Dictionary<string, string> fieldMappings,
        List<Field> fieldsToTranslate,
        dynamic targetLocales,
        string[] targetLocaleCodes,
        dynamic defaultLocale,
        Dictionary<string, CuteLanguage> translationConfiguration,
        Settings settings,
        SemaphoreSlim throttler,
        SemaphoreSlim updateSemaphore,
        ProgressTask taskTranslate)
    {
        long symbols = 0;
        bool needsPublish = false;
        var failures = new Dictionary<string, List<string>>();
        var allTranslationRequests = new List<(string text, string sourceLocale, CuteLanguage targetLanguage, string targetField, TranslationService service, TranslationService? fallbackService, string entryId)>();
        var entryFlatData = new Dictionary<string, (Entry<JObject> originalEntry, Dictionary<string, object?> flatEntry)>();

        // First pass: collect all translation requests
        foreach (var entry in entries)
        {
            var entryId = entry.SystemProperties.Id;
            var flatEntry = serializer.SerializeEntry(entry);
            entryFlatData[entryId] = (entry, new Dictionary<string, object?>(flatEntry));

            var defaultLocaleFieldNames = flatEntry.Keys
                .Where(k => k.Contains($".{defaultLocale.Code}"))
                .ToArray();

            foreach (var defaultLocaleFieldName in defaultLocaleFieldNames)
            {
                if (!fieldsToTranslate.Any(f => defaultLocaleFieldName.StartsWith($"{f.Id}.")))
                    continue;

                var flatEntryDefaultLocaleValue = flatEntry[defaultLocaleFieldName];
                var defaultLocaleFieldValue = flatEntryDefaultLocaleValue?.ToString();

                if (flatEntryDefaultLocaleValue is not string || string.IsNullOrEmpty(defaultLocaleFieldValue))
                {
                    taskTranslate.Increment(targetLocales.Count);
                    continue;
                }

                foreach (var targetLocale in targetLocales)
                {
                    if (!translationConfiguration.TryGetValue(targetLocale.Code, out CuteLanguage? cuteLanguage) || cuteLanguage == null)
                    {
                        _console.WriteAlert($"No translation configuration found for locale {targetLocale.Code}");
                        continue;
                    }

                    var targetLocaleFieldName = defaultLocaleFieldName.Replace($".{defaultLocale.Code}", $".{targetLocale.Code}");
                    
                    if (!flatEntry.TryGetValue(targetLocaleFieldName, out var flatEntryTargetLocaleValue) || 
                        flatEntryTargetLocaleValue is null || 
                        string.IsNullOrEmpty(flatEntryTargetLocaleValue.ToString()))
                    {
                        symbols += defaultLocaleFieldValue.Length;
                        TranslationService tService;
                        if (!Enum.TryParse(cuteLanguage!.TranslationService, out tService))
                        {
                            tService = TranslationService.GPT4o;
                        }
                        
                        allTranslationRequests.Add((
                            defaultLocaleFieldValue,
                            defaultLocale.Code,
                            cuteLanguage,
                            targetLocaleFieldName,
                            tService,
                            settings.FallbackService,
                            entryId
                        ));
                    }
                    
                    taskTranslate.Increment(1);
                }
            }
        }

        // Batch process translations
        if (allTranslationRequests.Count > 0)
        {
            var translationRequestsForBatch = allTranslationRequests
                .Select(r => (r.text, r.sourceLocale, r.targetLanguage, r.targetField, r.service, r.fallbackService))
                .ToList();
            
            var translationResults = await BatchTranslateFields(translationRequestsForBatch, throttler);
            var resultsByEntryAndField = new Dictionary<string, Dictionary<string, (string? translatedText, bool success)>>();

            // Map results back to entries
            for (int i = 0; i < allTranslationRequests.Count && i < translationResults.Count; i++)
            {
                var request = allTranslationRequests[i];
                var result = translationResults[i];
                
                if (!resultsByEntryAndField.ContainsKey(request.entryId))
                    resultsByEntryAndField[request.entryId] = new Dictionary<string, (string?, bool)>();
                    
                // CRITICAL FIX: Use request.targetField instead of result.targetField to maintain correct field mapping
                resultsByEntryAndField[request.entryId][request.targetField] = (result.translatedText, result.success);
            }

            // Process results and update entries
            var updateTasks = new List<Task>();
            
            foreach (var entryResult in resultsByEntryAndField)
            {
                var entryId = entryResult.Key;
                var (originalEntry, flatEntry) = entryFlatData[entryId];
                var entryChanged = false;

                foreach (var fieldResult in entryResult.Value)
                {
                    var targetField = fieldResult.Key;
                    var (translatedText, success) = fieldResult.Value;

                    if (success && !string.IsNullOrEmpty(translatedText))
                    {
                        flatEntry[targetField] = translatedText;
                        entryChanged = true;
                    }
                    else
                    {
                        if (!failures.ContainsKey(entryId))
                            failures[entryId] = new List<string>();
                        failures[entryId].Add(targetField);
                    }
                }

                if (entryChanged)
                {
                    needsPublish = true;
                    updateTasks.Add(UpdateEntryAsync(entryId, originalEntry, flatEntry, serializer, fieldsToTranslate, targetLocaleCodes, updateSemaphore));
                }
            }

            // Wait for all entry updates to complete
            await Task.WhenAll(updateTasks);
        }

        return (symbols, needsPublish, failures);
    }

    private async Task UpdateEntryAsync(
        string entryId,
        Entry<JObject> originalEntry,
        Dictionary<string, object?> flatEntry,
        EntrySerializer serializer,
        List<Field> fieldsToTranslate,
        string[] targetLocaleCodes,
        SemaphoreSlim updateSemaphore)
    {
        await updateSemaphore.WaitAsync();
        
        try
        {
            var cloudEntry = await ContentfulConnection.GetManagementEntryAsync(entryId);
            var deserializedEntry = serializer.DeserializeEntry(new Dictionary<string, object?>(flatEntry));

            foreach (var field in fieldsToTranslate)
            {
                foreach (var localeCode in targetLocaleCodes)
                {
                    if (cloudEntry.Fields[field.Id] is null)
                    {
                        cloudEntry.Fields[field.Id] = new JObject();
                    }
                    cloudEntry.Fields[field.Id][localeCode] = deserializedEntry.Fields[field.Id]![localeCode];
                }
            }

            await ContentfulConnection.CreateOrUpdateEntryAsync(cloudEntry, originalEntry.SystemProperties.Version);
        }
        finally
        {
            updateSemaphore.Release();
        }
    }

    private ITranslator GetCachedTranslator(TranslationService service)
    {
        return _translatorCache.GetOrAdd(service, s => _translateFactory.Create(s));
    }

    private async Task<string?> GetCachedTranslation(string text, string from, string to, TranslationService service, CuteLanguage? targetLanguage = null, Dictionary<string, string>? glossary = null)
    {
        var cacheKey = $"{from}-{to}-{service}";
        var textCache = _translationCache.GetOrAdd(cacheKey, _ => new ConcurrentDictionary<string, string>());
        
        if (textCache.TryGetValue(text, out var cached))
            return cached;

        var translator = GetCachedTranslator(service);
        string? translation = null;
        
        try
        {
            if (targetLanguage != null)
            {
                translation = await translate(translator, text, from, targetLanguage);
            }
            else
            {
                var response = await translator.Translate(text, from, to, glossary);
                translation = response?.Text;
            }
        }
        catch (Exception ex)
        {
            _console.WriteAlert($"Error translating text: {ex.Message}");
            return null;
        }

        if (!string.IsNullOrEmpty(translation))
        {
            textCache[text] = translation;
        }

        return translation;
    }

    private Dictionary<string, string> BuildFieldMappings(string defaultLocale, dynamic targetLocales, IEnumerable<Field> fieldsToTranslate)
    {
        var mappings = new Dictionary<string, string>();
        
        foreach (var field in fieldsToTranslate)
        {
            foreach (var targetLocale in targetLocales)
            {
                var sourceKey = $"{field.Id}.{defaultLocale}";
                var targetKey = $"{field.Id}.{targetLocale.Code}";
                mappings[sourceKey] = targetKey;
            }
        }
        
        return mappings;
    }

    private async Task FlushPendingUpdates(SemaphoreSlim updateSemaphore)
    {
        if (_pendingUpdates.Count == 0) return;

        var updateTasks = _pendingUpdates.Select(async update =>
        {
            await updateSemaphore.WaitAsync();
            try
            {
                await ContentfulConnection.CreateOrUpdateEntryAsync(update.entry, update.version);
            }
            finally
            {
                updateSemaphore.Release();
            }
        });

        await Task.WhenAll(updateTasks);
        _pendingUpdates.Clear();
    }

    private async Task<List<(string targetField, string? translatedText, bool success)>> BatchTranslateFields(
        List<(string text, string sourceLocale, CuteLanguage targetLanguage, string targetField, TranslationService service, TranslationService? fallbackService)> translationRequests,
        SemaphoreSlim throttler)
    {
        // Group by service and target language for batch processing
        var groupedRequests = translationRequests
            .GroupBy(r => new { r.service, r.targetLanguage.Iso2Code })
            .ToList();

        var allResults = new List<(string targetField, string? translatedText, bool success)>();

        foreach (var group in groupedRequests)
        {
            var requests = group.ToList();
            var batchTasks = new List<Task<(string targetField, string? translatedText, bool success)>>();

            // Process in smaller batches to avoid overwhelming the translation service
            for (int i = 0; i < requests.Count; i += TRANSLATION_BATCH_SIZE)
            {
                var batch = requests.Skip(i).Take(TRANSLATION_BATCH_SIZE).ToList();
                
                foreach (var request in batch)
                {
                    batchTasks.Add(TranslateFieldWithCache(
                        request.text,
                        request.sourceLocale,
                        request.targetLanguage,
                        request.targetField,
                        request.service,
                        request.fallbackService,
                        throttler));
                }
            }

            var batchResults = await Task.WhenAll(batchTasks);
            allResults.AddRange(batchResults);
        }

        return allResults;
    }

    private async Task<(string targetField, string? translatedText, bool success)> TranslateFieldWithCache(
        string text,
        string sourceLocale,
        CuteLanguage targetLanguage,
        string targetField,
        TranslationService service,
        TranslationService? fallbackService,
        SemaphoreSlim throttler)
    {
        await throttler.WaitAsync();
        
        try
        {
            var translatedText = await GetCachedTranslation(text, sourceLocale, targetLanguage.Iso2Code, service, targetLanguage);
            
            if (string.IsNullOrEmpty(translatedText) && fallbackService != null && service != fallbackService)
            {
                translatedText = await GetCachedTranslation(text, sourceLocale, targetLanguage.Iso2Code, fallbackService.Value);
            }

            return (targetField, translatedText, !string.IsNullOrEmpty(translatedText));
        }
        finally
        {
            throttler.Release();
        }
    }
}
