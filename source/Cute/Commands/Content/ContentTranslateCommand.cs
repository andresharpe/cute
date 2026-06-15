using Contentful.Core.Models;
using Contentful.Core.Models.Management;
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
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;

namespace Cute.Commands.Content;

public class ContentTranslateCommand(IConsoleWriter console, ILogger<ContentTranslateCommand> logger,
    AppSettings appSettings, TranslateFactory translateFactory, HttpClient httpClient) : BaseLoggedInCommand<ContentTranslateCommand.Settings>(console, logger, appSettings)
{    
    private readonly TranslateFactory _translateFactory = translateFactory;
    private readonly HttpClient _httpClient = httpClient;
    private readonly ConcurrentDictionary<TranslationService, ITranslator> _translatorCache = new();
    private readonly List<(string entryId, Entry<dynamic> entry, int version)> _pendingUpdates = new();
    private bool useCustomModel = false;
    private Dictionary<string, List<string>>? _countryLocaleCache;
    private Dictionary<string, List<string>>? _entryCountryLocaleCache;

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

        [CommandOption("--entry-batch-size")]
        [Description("Indicates how many concurrent calls can be made to a translation service for a single entry. Default is 10")]
        public int EntryBatchSize { get; set; } = 10;

        [CommandOption("--fallback-service")]
        [Description("Fallback translation service (Azure, Google, Deepl, GPT4o), in case configured one doesn't return a value. Will translate without a custom model and glossary")]
        public TranslationService? FallbackService { get; set; } = null;

        [CommandOption("--use-country-locale")]
        [Description("When enabled, determines target locales per entry based on its linked dataCountry")]
        public bool UseCountryLocale { get; set; } = false;
    }
    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var contentType = await GetContentTypeOrThrowError(settings.ContentTypeId);
        var defaultLocale = await ContentfulConnection.GetDefaultLocaleAsync();
        useCustomModel = settings.UseCustomModel;

        var fieldsToTranslate = contentType.Fields.Where(f => f.Localized).ToList();
        if(settings.Fields?.Length > 0)
        {
            fieldsToTranslate = fieldsToTranslate.Where(f => settings.Fields.Contains(f.Id)).ToList();
        }

        var contentLocales = await ContentfulConnection.GetLocalesAsync();
        var allContentLocales = new ContentLocales(contentLocales.Select(c => c.Code).ToArray(), defaultLocale.Code);

        var allNonDefaultLocales = contentLocales.Where(k => k.Code != defaultLocale.Code).ToList();
        var targetLocales = new List<Locale>(allNonDefaultLocales);
        if(settings.Locales?.Length > 0)
        {
            targetLocales = targetLocales.Where(k => settings.Locales.Contains(k.Code)).ToList();
        }

        var localesExplicitlyPassed = targetLocales.Count != allNonDefaultLocales.Count;

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

        if (settings.UseCountryLocale)
        {
            await BuildCountryLocaleCachesAsync(settings, translationConfiguration, defaultLocale.Code);
        }

        Dictionary<string, Dictionary<string, string>>? glossary = null;

        if (settings.UseGlossary)
        {
            var glossaryEnumerator = ContentfulConnection.GetManagementEntries<Entry<CuteTranslationGlossary>>("cuteTranslationGlossary");

            var glossaryLocales = settings.UseCountryLocale ? allNonDefaultLocales : targetLocales;
            glossary = glossaryLocales.ToDictionary(x => x.Code, x => new Dictionary<string, string>());

            await foreach (var (entry, total) in glossaryEnumerator!)
            {
                if (!entry.Fields.Title.TryGetValue(defaultLocale.Code, out var key) || string.IsNullOrEmpty(key))
                {
                    continue;
                }

                foreach (var targetLocale in glossaryLocales)
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

        try
        {
            // Create semaphores to limit concurrent operations
            var throttler = new SemaphoreSlim(settings.MaxConcurrency);
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
                    var progressLocaleCount = settings.UseCountryLocale ? allNonDefaultLocales.Count : targetLocales.Count;

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
                            taskTranslate.MaxValue = total * progressLocaleCount * fieldsToTranslate.Count;
                            totalEntries = total;
                        }

                        entryBatch.Add(entry);
                        
                        if (entryBatch.Count >= settings.EntryBatchSize)
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
                                taskTranslate,
                                glossary,
                                allNonDefaultLocales,
                                localesExplicitlyPassed,
                                progressLocaleCount);
                            
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
                            taskTranslate,
                            glossary,
                            allNonDefaultLocales,
                            localesExplicitlyPassed,
                            progressLocaleCount);
                        
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
                    await FlushPendingUpdates(throttler);
                    
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
                        .WithApplyChanges(!settings.NoPublish)
                        .WithErrorThreshold(settings.BulkPublishErrorThreshold),
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
        ProgressTask taskTranslate,
        Dictionary<string, Dictionary<string, string>>? glossary = null,
        List<Locale>? allNonDefaultLocales = null,
        bool localesExplicitlyPassed = false,
        int progressLocaleCount = 0)
    {
        long symbols = 0;
        bool needsPublish = false;
        var failures = new Dictionary<string, List<string>>();
        var allTranslationRequests = new List<(string text, string sourceLocale, CuteLanguage targetLanguage, string targetField, TranslationService service, TranslationService? fallbackService, string entryId, string requestId)>();
        var entryFlatData = new Dictionary<string, (Entry<JObject> originalEntry, Dictionary<string, object?> flatEntry)>();
        var entryResolvedLocaleCodes = new Dictionary<string, string[]>();

        var useCountryLocale = _entryCountryLocaleCache != null && allNonDefaultLocales != null;
        var maxLocaleCount = progressLocaleCount > 0 ? progressLocaleCount : ((List<Locale>)targetLocales).Count;

        // First pass: collect all translation requests
        foreach (var entry in entries)
        {
            var entryId = entry.SystemProperties.Id;
            var flatEntry = serializer.SerializeEntry(entry);
            entryFlatData[entryId] = (entry, new Dictionary<string, object?>(flatEntry));

            // Resolve per-entry target locales
            var entryTargetLocales = useCountryLocale
                ? GetTargetLocalesForEntry(entryId, (List<Locale>)targetLocales, allNonDefaultLocales!, localesExplicitlyPassed)
                : (List<Locale>)targetLocales;

            if (entryTargetLocales.IsNullOrEmpty())
            {
                taskTranslate.Increment(maxLocaleCount);
                continue;
            }

            entryResolvedLocaleCodes[entryId] = entryTargetLocales.Select(l => l.Code).ToArray();

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
                    taskTranslate.Increment(maxLocaleCount);
                    continue;
                }

                foreach (var targetLocale in entryTargetLocales)
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
                            tService = TranslationService.AzureOpenAi;
                        }
                        
                        var requestId = Guid.NewGuid().ToString();
                        allTranslationRequests.Add((
                            defaultLocaleFieldValue,
                            defaultLocale.Code,
                            cuteLanguage,
                            targetLocaleFieldName,
                            tService,
                            settings.FallbackService,
                            entryId,
                            requestId
                        ));
                    }
                    
                    taskTranslate.Increment(1);
                }

                // Pad progress for entries with fewer locales
                var localeDiff = maxLocaleCount - entryTargetLocales.Count;
                if (localeDiff > 0)
                {
                    taskTranslate.Increment(localeDiff);
                }
            }
        }

        // Batch process translations
        if (allTranslationRequests.Count > 0)
        {
            var translationRequestsForBatch = allTranslationRequests
                .Select(r => (r.text, r.sourceLocale, r.targetLanguage, r.targetField, r.service, r.fallbackService, r.requestId, r.entryId))
                .ToList();
            
            var translationResults = await BatchTranslateFields(translationRequestsForBatch, throttler, glossary);
            
            var resultsByEntryAndField = new Dictionary<string, Dictionary<string, (string? translatedText, bool success)>>();
            
            // Create lookup dictionary for requests by requestId
            var requestLookup = allTranslationRequests.ToDictionary(r => r.requestId, r => r);

            // Map results back to entries using requestId
            foreach (var result in translationResults)
            {
                if (requestLookup.TryGetValue(result.requestId, out var request))
                {
                    if (!resultsByEntryAndField.ContainsKey(request.entryId))
                        resultsByEntryAndField[request.entryId] = new Dictionary<string, (string?, bool)>();
                    
                    // Use request.targetField to ensure correct field mapping
                    resultsByEntryAndField[request.entryId][request.targetField] = (result.translatedText, result.success);
                }
                else
                {
                    Console.WriteLine($"[ERROR] RequestId {result.requestId} not found in lookup!");
                }
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
                    var perEntryLocaleCodes = entryResolvedLocaleCodes.TryGetValue(entryId, out var codes) ? codes : targetLocaleCodes;
                    updateTasks.Add(UpdateEntryAsync(entryId, originalEntry, flatEntry, serializer, fieldsToTranslate, perEntryLocaleCodes, throttler));
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

    private async Task<TranslationResponse[]?> TranslateTextMultiLanguage(string text, string from, TranslationService service, List<CuteLanguage> targetLanguages, TranslationService? fallbackService = null, Dictionary<string, Dictionary<string, string>>? glossaries = null)
    {
        // Create a NEW translator instance for each call to avoid state issues with concurrent requests
        // The ChatClient may have state that gets confused with concurrent requests
        var translator = _translateFactory.Create(service);
        TranslationResponse[]? translations = null;
        
        try
        {
            if (useCustomModel)
            {
                // Use the multi-language translation method
                translations = await translator.TranslateWithCustomModel(text, from, targetLanguages, glossaries);
            }
            else
            {
                translations = await translator.Translate(text, from, targetLanguages, glossaries);
            }
        }
        catch (Exception ex)
        {
            _console.WriteAlert($"Error translating text to multiple languages: {ex.Message}");
            return translations;
        }

        // Try fallback service if primary failed
        if ((translations == null || translations.Length != targetLanguages.Count) && fallbackService != null && service != fallbackService)
        {
            try
            {
                var translatedLanguages = translations?.Select(t => t.TargetLanguage).ToHashSet() ?? new HashSet<string>();

                var fallbackTranslator = _translateFactory.Create(fallbackService.Value);
                var languageCodes = targetLanguages.Where(l => !translatedLanguages.Contains(l.Iso2Code)).Select(l => l.Iso2Code).ToArray();
                var fallBackTranslations = await fallbackTranslator.Translate(text, from, languageCodes);
                translations = (fallBackTranslations ?? Array.Empty<TranslationResponse>()).Concat(translations ?? Array.Empty<TranslationResponse>()).ToArray();
            }
            catch (Exception ex)
            {
                _console.WriteAlert($"Error translating text with fallback service: {ex.Message}");
            }
        }

        return translations;
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

    private async Task<List<(string targetField, string? translatedText, bool success, string requestId)>> BatchTranslateFields(
        List<(string text, string sourceLocale, CuteLanguage targetLanguage, string targetField, TranslationService service, TranslationService? fallbackService, string requestId, string entryId)> translationRequests,
        SemaphoreSlim throttler,
        Dictionary<string, Dictionary<string, string>>? glossaries = null)
    {
        // Group by text, sourceLocale, service, entryId, and field base name to translate same field from same entry to multiple languages at once
        // Important: Include entryId to avoid mixing translations between different entries with same content
        var groupedRequests = translationRequests
            .GroupBy(r => new { 
                r.text, 
                r.sourceLocale, 
                r.service, 
                r.fallbackService, 
                r.entryId,
                fieldBaseName = r.targetField.Substring(0, r.targetField.LastIndexOf('.')) // Extract field name without locale
            })
            .ToList();

        var allResults = new ConcurrentBag<(string targetField, string? translatedText, bool success, string requestId)>();
        var batchTasks = new List<Task>();

        foreach (var group in groupedRequests)
        {
            // Capture loop variables to avoid closure issues
            var requests = group.ToList();
            var targetLanguages = requests.Select(r => r.targetLanguage).ToList();
            var textToTranslate = group.Key.text; // Create local copy
            var sourceLocaleCode = group.Key.sourceLocale; // Create local copy
            var translationService = group.Key.service; // Create local copy
            var fallbackTranslationService = group.Key.fallbackService; // Create local copy
            
            var entryIds = string.Join(", ", requests.Select(r => r.entryId).Distinct());

            // Translate to all target languages at once
            batchTasks.Add(Task.Run(async () =>
            {
                await throttler.WaitAsync();
                try
                {
                    var translations = await TranslateTextMultiLanguage(textToTranslate, sourceLocaleCode, translationService, targetLanguages, fallbackTranslationService, glossaries);
                    
                    // Map translations back to request IDs - each request gets its own translation
                    foreach (var request in requests)
                    {
                        var translation = translations?.FirstOrDefault(t => t.TargetLanguage == request.targetLanguage.Iso2Code);
                        var translatedText = translation?.Text;
                        
                        // Use ConcurrentBag to avoid lock contention
                        allResults.Add((request.targetField, translatedText, !string.IsNullOrEmpty(translatedText), request.requestId));
                    }
                }
                catch (Exception ex)
                {
                    _console.WriteAlert($"Error in multi-language translation: {ex.Message}");
                    
                    // Add failed results for all requests in this group
                    foreach (var request in requests)
                    {
                        allResults.Add((request.targetField, null, false, request.requestId));
                    }
                }
                finally
                {
                    throttler.Release();
                }
            }));
        }

        await Task.WhenAll(batchTasks);
        return allResults.ToList();
    }

    private async Task<List<(string FieldId, bool IsArray, string LinkedContentTypeId)>?> FindPathToDataCountry(string startContentTypeId)
    {
        var contentTypes = (await ContentfulConnection.GetContentTypesAsync())
            .ToDictionary(ct => ct.SystemProperties.Id);

        var visited = new HashSet<string> { startContentTypeId };
        var queue = new Queue<(string ContentTypeId, List<(string FieldId, bool IsArray, string LinkedContentTypeId)> Path)>();
        queue.Enqueue((startContentTypeId, new List<(string, bool, string)>()));

        while (queue.Count > 0)
        {
            var (currentTypeId, currentPath) = queue.Dequeue();

            if (!contentTypes.TryGetValue(currentTypeId, out var currentType))
                continue;

            foreach (var field in currentType.Fields)
            {
                var linkedTypeIds = new List<string>();
                bool isArray = false;

                if (field.Type == "Link" && field.LinkType == "Entry")
                {
                    linkedTypeIds.AddRange(field.Validations
                        .OfType<LinkContentTypeValidator>()
                        .SelectMany(v => v.ContentTypeIds));
                }
                else if (field.Type == "Array" && field.Items?.LinkType == "Entry")
                {
                    isArray = true;
                    linkedTypeIds.AddRange(field.Items.Validations
                        .OfType<LinkContentTypeValidator>()
                        .SelectMany(v => v.ContentTypeIds));
                }

                foreach (var linkedTypeId in linkedTypeIds)
                {
                    var newPath = new List<(string, bool, string)>(currentPath)
                    {
                        (field.Id, isArray, linkedTypeId)
                    };

                    if (linkedTypeId == "dataCountry")
                    {
                        return newPath;
                    }

                    if (!visited.Contains(linkedTypeId))
                    {
                        visited.Add(linkedTypeId);
                        queue.Enqueue((linkedTypeId, newPath));
                    }
                }
            }
        }

        return null;
    }

    private async Task BuildCountryLocaleCachesAsync(
        Settings settings,
        Dictionary<string, CuteLanguage> translationConfiguration,
        string defaultLocaleCode)
    {
        var pathToCountry = await FindPathToDataCountry(settings.ContentTypeId);
        if (pathToCountry == null || pathToCountry.Count == 0)
        {
            _console.WriteAlert($"No relation to dataCountry found for content type '{settings.ContentTypeId}'. UseCountryLocale will be ignored.");
            return;
        }

        await ProgressBars.Instance()
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                // --- Cache 1: country -> locales ---
                var taskCountry = ctx.AddTask($"{Emoji.Known.GlobeShowingEuropeAfrica}  Caching country locales (0 fetched)...");
                taskCountry.IsIndeterminate = true;

                var countryQuery = @"
                    query GetContent($locale: String, $preview: Boolean, $skip: Int, $limit: Int) {
                      dataCountryCollection(locale: $locale, preview: $preview, skip: $skip, limit: $limit) {
                        items {
                          sys { id }
                          dataLanguageEntriesCollection {
                            items {
                              iso2Code
                            }
                          }
                        }
                      }
                    }";

                _countryLocaleCache = new Dictionary<string, List<string>>();
                int countryCount = 0;

                await foreach (var country in ContentfulConnection.GraphQL.GetDataEnumerable(
                    countryQuery,
                    "data.dataCountryCollection.items",
                    defaultLocaleCode,
                    preview: true,
                    pageSize: 500))
                {
                    countryCount++;
                    var countryId = country.SelectToken("sys.id")?.Value<string>();
                    if (string.IsNullOrEmpty(countryId)) continue;

                    var languageItems = country.SelectToken("dataLanguageEntriesCollection.items") as JArray;
                    if (languageItems == null) continue;

                    var validIso2Codes = new List<string>();
                    foreach (var lang in languageItems)
                    {
                        var iso2Code = lang.SelectToken("iso2Code")?.Value<string>();
                        if (string.IsNullOrEmpty(iso2Code)) continue;

                        if (translationConfiguration.TryGetValue(iso2Code, out var cuteLanguage) && cuteLanguage.IsContentfulLocale)
                        {
                            validIso2Codes.Add(iso2Code);
                        }
                    }

                    _countryLocaleCache[countryId] = validIso2Codes;
                    taskCountry.Description = $"{Emoji.Known.GlobeShowingEuropeAfrica}  Caching country locales ({countryCount} fetched)...";
                }

                taskCountry.Description = $"{Emoji.Known.GlobeShowingEuropeAfrica}  Cached {_countryLocaleCache.Count} country locale mappings";
                taskCountry.IsIndeterminate = false;
                taskCountry.Value = 100;
                taskCountry.MaxValue = 100;
                taskCountry.StopTask();

                // --- Cache 2: entry -> country locales ---
                var taskEntries = ctx.AddTask($"{Emoji.Known.Link}  Caching entry country mappings (0 fetched)...");
                taskEntries.IsIndeterminate = true;

                var entryQuerySb = new StringBuilder();
                entryQuerySb.AppendLine("query GetContent($locale: String, $preview: Boolean, $skip: Int, $limit: Int) {");
                entryQuerySb.AppendLine($"  {settings.ContentTypeId}Collection(locale: $locale, preview: $preview, skip: $skip, limit: $limit) {{");
                entryQuerySb.AppendLine("    items {");
                entryQuerySb.AppendLine("      sys { id }");

                var indent = "      ";
                for (int i = 0; i < pathToCountry.Count; i++)
                {
                    var (fieldId, isArray, _) = pathToCountry[i];
                    if (isArray)
                    {
                        entryQuerySb.AppendLine($"{indent}{fieldId}Collection {{");
                        entryQuerySb.AppendLine($"{indent}  items {{");
                        indent += "    ";
                    }
                    else
                    {
                        entryQuerySb.AppendLine($"{indent}{fieldId} {{");
                        indent += "  ";
                    }

                    if (i == pathToCountry.Count - 1)
                    {
                        entryQuerySb.AppendLine($"{indent}sys {{ id }}");
                    }
                }

                for (int i = pathToCountry.Count - 1; i >= 0; i--)
                {
                    var (_, isArray, _) = pathToCountry[i];
                    if (isArray)
                    {
                        indent = indent[..^4];
                        entryQuerySb.AppendLine($"{indent}  }}");
                        entryQuerySb.AppendLine($"{indent}}}");
                    }
                    else
                    {
                        indent = indent[..^2];
                        entryQuerySb.AppendLine($"{indent}}}");
                    }
                }

                entryQuerySb.AppendLine("    }");
                entryQuerySb.AppendLine("  }");
                entryQuerySb.AppendLine("}");

                _entryCountryLocaleCache = new Dictionary<string, List<string>>();
                int entryCount = 0;

                await foreach (var entry in ContentfulConnection.GraphQL.GetDataEnumerable(
                    entryQuerySb.ToString(),
                    $"data.{settings.ContentTypeId}Collection.items",
                    defaultLocaleCode,
                    preview: true,
                    pageSize: 500))
                {
                    entryCount++;
                    var entryId = entry.SelectToken("sys.id")?.Value<string>();
                    if (string.IsNullOrEmpty(entryId)) continue;

                    var countryIds = ExtractCountryIds(entry, pathToCountry, 0);
                    var entryLocales = new HashSet<string>();

                    foreach (var countryId in countryIds)
                    {
                        if (_countryLocaleCache.TryGetValue(countryId, out var locales))
                        {
                            foreach (var locale in locales)
                            {
                                entryLocales.Add(locale);
                            }
                        }
                    }

                    _entryCountryLocaleCache[entryId] = entryLocales.ToList();
                    taskEntries.Description = $"{Emoji.Known.Link}  Caching entry country mappings ({entryCount} fetched)...";
                }

                taskEntries.Description = $"{Emoji.Known.Link}  Cached {_entryCountryLocaleCache.Count} entry locale mappings";
                taskEntries.IsIndeterminate = false;
                taskEntries.Value = 100;
                taskEntries.MaxValue = 100;
                taskEntries.StopTask();
            });
    }

    private static List<string> ExtractCountryIds(
        JToken token,
        List<(string FieldId, bool IsArray, string LinkedContentTypeId)> path,
        int depth)
    {
        if (depth >= path.Count)
        {
            var id = token.SelectToken("sys.id")?.Value<string>();
            return id != null ? [id] : [];
        }

        var (fieldId, isArray, _) = path[depth];
        var result = new List<string>();

        if (isArray)
        {
            if (token.SelectToken($"{fieldId}Collection.items") is JArray items)
            {
                foreach (var item in items)
                {
                    result.AddRange(ExtractCountryIds(item, path, depth + 1));
                }
            }
        }
        else
        {
            var child = token.SelectToken(fieldId);
            if (child != null && child.Type != JTokenType.Null)
            {
                result.AddRange(ExtractCountryIds(child, path, depth + 1));
            }
        }

        return result;
    }

    private List<Locale> GetTargetLocalesForEntry(
        string entryId,
        List<Locale> targetLocales,
        List<Locale> allNonDefaultLocales,
        bool localesExplicitlyPassed)
    {
        if (_entryCountryLocaleCache == null || !_entryCountryLocaleCache.TryGetValue(entryId, out var countryLocaleCodes))
        {
            return targetLocales;
        }

        var countryLocales = allNonDefaultLocales
            .Where(l => countryLocaleCodes.Contains(l.Code))
            .ToList();

        if (!localesExplicitlyPassed)
        {
            // Locales were not explicitly passed — use only country-specific locales
            return countryLocales;
        }

        // Merge: targetLocales UNION country-specific locales
        var merged = new List<Locale>(targetLocales);
        foreach (var locale in countryLocales)
        {
            if (!merged.Any(l => l.Code == locale.Code))
            {
                merged.Add(locale);
            }
        }

        return merged;
    }
}
