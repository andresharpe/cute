using Azure;
using Azure.AI.OpenAI;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Contentful.Core.Search;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Services;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Scriban;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Dynamic;
using System.Text;
using Text = Spectre.Console.Text;

namespace Cute.Commands;

// generate --prompt-id DataGeo.BusinessRationale
// generate --prompt-id ContentGeo.BusinessRationale

public class GenerateCommand : LoggedInCommand<GenerateCommand.Settings>
{
    private readonly ILogger<GenerateCommand> _logger;

    public GenerateCommand(IConsoleWriter console, IPersistedTokenCache tokenCache, ILogger<GenerateCommand> logger)
        : base(console, tokenCache, logger)
    {
        _logger = logger;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--prompt-content-type")]
        [Description("The id of the content type containing prompts. Default is 'metaPrompts'.")]
        public string PromptContentType { get; set; } = "metaPrompt";

        [CommandOption("-f|--prompt-id-field")]
        [Description("The id of the field that contains the prompt key/title/id. Default is 'key'.")]
        public string PromptIdField { get; set; } = "key";

        [CommandOption("-i|--prompt-id")]
        [Description("The id of the Contentful prompt entry to generate prompts from.")]
        public string PromptId { get; set; } = default!;

        [CommandOption("-o|--output-content-type-field")]
        [Description("The field containing the id of the Contentful content type to generate content for.")]
        public string OutputContentType { get; set; } = "promptOutputContentType";

        [CommandOption("-t|--output-content-field")]
        [Description("The target field of the Contentful content type to generate content for.")]
        public string OutputContentField { get; set; } = "promptOutputContentField";

        [CommandOption("-s|--system-message-field")]
        [Description("The field containing the system prompt for the LLM.")]
        public string SystemMessageField { get; set; } = "systemMessage";

        [CommandOption("-p|--prompt-field")]
        [Description("The field containing the prompt template for the LLM.")]
        public string PromptField { get; set; } = "prompt";

        [CommandOption("-m|--temperature-field")]
        [Description("The field containing temperature setting for the LLM.")]
        public string TemperatureField { get; set; } = "temperature";

        [CommandOption("-a|--frequency-penalty-field")]
        [Description("The field containing frequency penalty setting for the LLM.")]
        public string FrequencyPenaltyField { get; set; } = "frequencyPenalty";

        [CommandOption("-g|--generator-language-field")]
        [Description("The field containing language target for generated content.")]
        public string GeneratorLanguageField { get; set; } = "generatorTargetLanguage";

        [CommandOption("-n|--translator-language-field")]
        [Description("The field containing language targets for translated content.")]
        public string TranslatorLanguagesField { get; set; } = "translatorTargetLanguages";

        [CommandOption("-e|--entry-id")]
        [Description("The field containing language targets for translated content.")]
        public string? EntryId { get; set; } = null;

        [CommandOption("-r|--related-entry-id")]
        [Description("The field containing language targets for translated content.")]
        public string? RelatedEntryId { get; set; } = null;

        [CommandOption("-l|--limit")]
        [Description("The total number of entries to generate content for before stopping. Default is five.")]
        public int Limit { get; set; } = 5;

        [CommandOption("-k|--skip")]
        [Description("The total number of entries to skip before starting. Default is zero.")]
        public int Skip { get; set; } = 0;

        [CommandOption("-d|--delay")]
        [Description("The delay in milliseconds between retrieving generated tokens.")]
        public int Delay { get; set; } = 20;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.PromptId))
        {
            return ValidationResult.Error($"No prompt identifier (--prompt-id) specified.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        var locales = await ContentfulManagementClient.GetLocalesCollection();

        var defaultLocale = locales
            .First(l => l.Default)
            .Code;

        var allLocaleCodes = locales.Select(locales => locales.Code).ToHashSet();

        var promptQuery = new QueryBuilder<Dictionary<string, object?>>()
             .ContentTypeIs(settings.PromptContentType)
             .Limit(1)
             .FieldEquals($"fields.{settings.PromptIdField}", settings.PromptId)
             .Build();

        var promptEntries = await ContentfulManagementClient.GetEntriesCollection<Entry<JObject>>(promptQuery);

        if (!promptEntries.Any())
        {
            throw new CliException($"No prompt with title '{settings.PromptId}' found.");
        }

        var promptEntry = promptEntries.First();

        var promptContentTypeId = promptEntry.Fields[settings.OutputContentType]?[defaultLocale]?.Value<string>()
            ?? throw new CliException($"Prompt '{settings.PromptId}' does not contain a valid contentTypeId");

        var promptContentFieldId = promptEntry.Fields[settings.OutputContentField]?[defaultLocale]?.Value<string>()
            ?? throw new CliException($"Prompt '{settings.PromptId}' does not contain a valid contentFieldId");

        var promptSystemMessage = promptEntry.Fields[settings.SystemMessageField]?[defaultLocale]?.Value<string>()
            ?? throw new CliException($"Prompt '{settings.PromptId}' does not contain a valid systemMessage");

        var promptMainPrompt = promptEntry.Fields[settings.PromptField]?[defaultLocale]?.Value<string>()
            ?? throw new CliException($"Prompt '{settings.PromptId}' does not contain a valid prompt");

        var promptTemperature = promptEntry.Fields[settings.TemperatureField]?[defaultLocale]?.Value<float>()
            ?? throw new CliException($"Prompt '{settings.PromptId}' does not contain a valid temperature");

        var promptFrequencyPenalty = promptEntry.Fields[settings.FrequencyPenaltyField]?[defaultLocale]?.Value<float>()
            ?? throw new CliException($"Prompt '{settings.PromptId}' does not contain a valid frequency penalty");

        var generatorLanguage = promptEntry.Fields[settings.GeneratorLanguageField]?[defaultLocale]?.ToObject<Reference>()
            ?? throw new CliException($"Prompt '{settings.GeneratorLanguageField}' does not contain a valid language reference");

        var translatorLanguages = promptEntry.Fields[settings.TranslatorLanguagesField]?[defaultLocale]?.ToObject<Reference[]>()
            ?? [];

        var contentType = await ContentfulManagementClient.GetContentType(promptContentTypeId);

        var contentTargetField = (contentType.Fields.FirstOrDefault(f => f.Id.Equals(promptContentFieldId)))
            ?? throw new CliException($"'{promptContentFieldId}' does not exist in content type '{contentType.SystemProperties.Id}'");

        // Generator language settings

        var generatorLanguageEntry = (JObject)(await ContentfulManagementClient.GetEntry(generatorLanguage.Sys.Id)).Fields;
        var generatorLanguageCode = generatorLanguageEntry["iso2code"]?[defaultLocale]?.ToString() ?? defaultLocale;
        var generatorLanguageName = generatorLanguageEntry["name"]?[defaultLocale]?.ToString();
        if (!allLocaleCodes.Contains(generatorLanguageCode) || generatorLanguageName is null)
        {
            throw new CliException($"Generator locale '{generatorLanguageCode}' does not exist in your Contentful space.");
        }

        // Translator language settings

        Dictionary<string, string> translatedLanguageCodeAndName = [];
        foreach (var language in translatorLanguages)
        {
            var translatorLanguageEntry = (JObject)(await ContentfulManagementClient.GetEntry(language.Sys.Id)).Fields;
            var translatorLanguageCode = translatorLanguageEntry["iso2code"]?[defaultLocale]?.ToString() ?? defaultLocale;
            var translatorLanguageName = translatorLanguageEntry["name"]?[defaultLocale]?.ToString();
            if (!allLocaleCodes.Contains(translatorLanguageCode) || translatorLanguageName is null)
            {
                throw new CliException($"Translator locale '{translatorLanguageCode}' does not exist in your Contentful space.");
            }
            translatedLanguageCodeAndName.Add(translatorLanguageCode, translatorLanguageName);
        }

        // Start Generator

        AzureOpenAIClient client = new(
          new Uri(_appSettings.OpenAiEndpoint),
          new AzureKeyCredential(_appSettings.OpenAiApiKey));

        var chatClient = client.GetChatClient(_appSettings.OpenAiDeploymentName);

        Action<QueryBuilder<ExpandoObject>>? queryConfigEntry =
            string.IsNullOrEmpty(settings.EntryId)
            ? null
            : b => b.LocaleIs(generatorLanguageCode).FieldEquals("sys.id", settings.EntryId);

        Action<QueryBuilder<ExpandoObject>>? queryConfigRelatedEntry = null;

        if (!string.IsNullOrEmpty(settings.RelatedEntryId))
        {
            var relatedEntry = await ContentfulManagementClient.GetEntry(settings.RelatedEntryId);

            var relatedContentType = relatedEntry.SystemProperties.ContentType.SystemProperties.Id;

            var relatedField = contentType.Fields
                .Where(f => f.Validations.Any(v => v is LinkContentTypeValidator vLink && vLink.ContentTypeIds.Contains(relatedContentType)))
                .FirstOrDefault()
                ?? throw new CliException($"Related entry of content type '{relatedContentType}' does not relate to '{contentType.SystemProperties.Id}'");

            queryConfigRelatedEntry = b => b.FieldEquals($"fields.{relatedField.Id}.sys.id", settings.RelatedEntryId);
        }

        var entries = ContentfulEntryEnumerator.DeliveryEntries(ContentfulClient, contentType.SystemProperties.Id, contentType.DisplayField,
            queryConfigurator: queryConfigEntry ?? queryConfigRelatedEntry);

        var allEntries = entries.ToBlockingEnumerable().ToList();

        var skipped = 0;
        var limit = 0;

        var generatorCompletionOptions = new ChatCompletionOptions()
        {
            Temperature = promptTemperature,
            MaxTokens = 800,
            FrequencyPenalty = promptFrequencyPenalty,
            PresencePenalty = 0,
        };

        await foreach (var (entry, _) in entries)
        {
            var fieldValue = GetPropertyValue(entry, promptContentFieldId)?.ToString();

            if (!string.IsNullOrEmpty(fieldValue))
            {
                continue;
            }

            if (skipped < settings.Skip)
            {
                skipped++;
                continue;
            }

            await GenerateContent(
                promptContentFieldId,
                promptSystemMessage,
                promptMainPrompt,
                contentType,
                generatorLanguageCode,
                chatClient,
                generatorCompletionOptions,
                entry,
                settings.Delay);

            if (++limit >= settings.Limit)
            {
                break;
            }
        }

        if (!contentTargetField.Localized)
        {
            return 0;
        }

        Action<QueryBuilder<Entry<JObject>>>? queryConfigFullEntry =
            string.IsNullOrEmpty(settings.EntryId)
            ? null
            : b => b.LocaleIs(generatorLanguageCode).FieldEquals("sys.id", settings.EntryId);

        Action<QueryBuilder<Entry<JObject>>>? queryConfigRelatedFullEntry = null;

        if (!string.IsNullOrEmpty(settings.RelatedEntryId))
        {
            var relatedEntry = await ContentfulManagementClient.GetEntry(settings.RelatedEntryId);

            var relatedContentType = relatedEntry.SystemProperties.ContentType.SystemProperties.Id;

            var relatedField = contentType.Fields
                .Where(f => f.Validations.Any(v => v is LinkContentTypeValidator vLink && vLink.ContentTypeIds.Contains(relatedContentType)))
                .FirstOrDefault()
                ?? throw new CliException($"Related entry of content type '{relatedContentType}' does not relate to '{contentType.SystemProperties.Id}'");

            queryConfigRelatedFullEntry = b => b.FieldEquals($"fields.{relatedField.Id}.sys.id", settings.RelatedEntryId);
        }

        var fullEntries = ContentfulEntryEnumerator.Entries(ContentfulManagementClient, contentType.SystemProperties.Id, contentType.DisplayField,
            queryConfigurator: queryConfigFullEntry ?? queryConfigRelatedFullEntry);

        skipped = 0;
        limit = 0;

        var translatorCompletionOptions = new ChatCompletionOptions()
        {
            Temperature = 0.0f,
            MaxTokens = 800,
            FrequencyPenalty = promptFrequencyPenalty,
            PresencePenalty = 0,
        };

        await foreach (var (fullEntry, _) in fullEntries)
        {
            if (fullEntry.SystemProperties.PublishedAt is null)
            {
                continue;
            }

            var fieldValue = fullEntry.Fields[promptContentFieldId]?[generatorLanguageCode]?.Value<string>();

            if (string.IsNullOrEmpty(fieldValue))
            {
                continue;
            }

            if (skipped < settings.Skip)
            {
                skipped++;
                continue;
            }

            await TranslateContent(
                promptContentFieldId,
                contentType,
                generatorLanguageCode,
                translatedLanguageCodeAndName,
                chatClient,
                translatorCompletionOptions,
                fullEntry,
                fieldValue,
                settings.Delay);

            if (++limit >= settings.Limit)
            {
                break;
            }
        }

        return 0;
    }

    private async Task GenerateContent(string promptContentFieldId, string promptSystemMessage,
        string promptMainPrompt, ContentType contentType, string generatorLanguageCode,
        ChatClient chatClient, ChatCompletionOptions generatorCompletionOptions, ExpandoObject entry,
        int delay)
    {
        _console.WriteBlankLine();

        _console.WriteHeading(GetPropertyValue(entry, contentType.DisplayField)?.ToString() ?? string.Empty);

        _console.WriteBlankLine();

        var prompt = ReplaceFields(promptMainPrompt, entry);

        _console.WriteRuler();
        _console.WriteDim(prompt);

        _console.WriteBlankLine();
        _console.WriteBlankLine();

        List<ChatMessage> messages = [
            new SystemChatMessage(promptSystemMessage),
            new UserChatMessage(prompt),
        ];

        _console.WriteBlankLine();

        var sb = new StringBuilder();

        await foreach (var part in chatClient.CompleteChatStreamingAsync(messages, generatorCompletionOptions))
        {
            if (part == null || part.ToString() == null) continue;

            foreach (var token in part.ContentUpdate)
            {
                sb.Append(token.Text);
                if (delay > 0)
                {
                    _console.Write(new Text(token.Text, Globals.StyleNormal));
                    await Task.Delay(delay);
                }
            }
        }

        var promptOutput = sb.ToString();

        if (delay == 0)
        {
            AnsiConsole.Write(new Rule() { Style = Globals.StyleDim });
            _console.WriteNormal(promptOutput);
        }

        var id = GetPropertyValue(entry, "$id")?.ToString();

        var objToUpdate = await ContentfulManagementClient!.GetEntry(id);

        var fieldDict = (JObject)objToUpdate.Fields;

        if (fieldDict[promptContentFieldId] == null)
        {
            fieldDict[promptContentFieldId] = new JObject(new JProperty(generatorLanguageCode, promptOutput));
        }
        else if (fieldDict[promptContentFieldId] is JObject existingValues)
        {
            if (existingValues[generatorLanguageCode] == null)
            {
                existingValues.Add(new JProperty(generatorLanguageCode, promptOutput));
            }
            else
            {
                existingValues[generatorLanguageCode] = promptOutput;
            }
        }

        _ = await ContentfulManagementClient.CreateOrUpdateEntry<dynamic>(objToUpdate.Fields,
            id: objToUpdate.SystemProperties.Id,
            version: objToUpdate.SystemProperties.Version);

        _ = await ContentfulManagementClient.PublishEntry(objToUpdate.SystemProperties.Id,
            objToUpdate.SystemProperties.Version!.Value + 1);

        _console.WriteBlankLine();
        _console.WriteBlankLine();
        _console.WriteRuler();
    }

    private async Task TranslateContent(string promptContentFieldId, ContentType contentType,
        string generatorLanguageCode, Dictionary<string, string> translatorLanguageCodeAndName,
        ChatClient chatClient, ChatCompletionOptions translatorCompletionOptions, Entry<JObject> fullEntry, string quotedText,
        int delay)
    {
        var promptSystemMessage = "You are a professional translator who pays attention to grammar and punctuation.";

        var promptTemplate = $$$""""
            Translate the quoted text into {{ languageName }}.
            Don't change the tone of the quoted text.
            Only output the translated text and nothing else.
            Omit the quotes around the quoted text in your output.
            """{{{quotedText}}}"""
            """";

        var updated = false;

        foreach (var (languageCode, languageName) in translatorLanguageCodeAndName)
        {
            if (!string.IsNullOrEmpty(fullEntry.Fields[promptContentFieldId]?[languageCode]?.Value<string>()))
            {
                continue;
            }

            var prompt = promptTemplate.Replace("{{ languageName }}", languageName);

            _console.WriteBlankLine();

            _console.WriteHeading(fullEntry.Fields[contentType.DisplayField]?[generatorLanguageCode]?.Value<string>() ?? string.Empty);

            AnsiConsole.Write(new Rule() { Style = Globals.StyleDim });
            _console.WriteDim(prompt);

            _console.WriteBlankLine();
            _console.WriteBlankLine();

            List<ChatMessage> messages = [
                new SystemChatMessage(promptSystemMessage),
                new UserChatMessage(prompt),
            ];

            _console.WriteBlankLine();

            var sb = new StringBuilder();

            await foreach (var part in chatClient.CompleteChatStreamingAsync(messages, translatorCompletionOptions))
            {
                if (part == null || part.ToString() == null) continue;

                foreach (var token in part.ContentUpdate)
                {
                    sb.Append(token.Text);
                    if (delay > 0)
                    {
                        _console.Write(new Text(token.Text, Globals.StyleNormal));
                        await Task.Delay(delay);
                    }
                }
            }

            var promptOutput = sb.ToString();

            if (delay == 0)
            {
                AnsiConsole.Write(new Rule() { Style = Globals.StyleDim });
                _console.WriteNormal(promptOutput);
            }

            var fieldDict = fullEntry.Fields;

            if (fieldDict[promptContentFieldId] == null)
            {
                fieldDict[promptContentFieldId] = new JObject(new JProperty(languageCode, promptOutput));
            }
            else if (fieldDict[promptContentFieldId] is JObject existingValues)
            {
                if (existingValues[languageCode] == null)
                {
                    existingValues.Add(new JProperty(languageCode, promptOutput));
                }
                else
                {
                    existingValues[languageCode] = promptOutput;
                }
            }

            _console.WriteBlankLine();
            _console.WriteBlankLine();
            _console.WriteRuler();

            updated = true;
        }

        if (updated)
        {
            _ = await ContentfulManagementClient!.CreateOrUpdateEntry<dynamic>(fullEntry.Fields,
                    id: fullEntry.SystemProperties.Id,
                    version: fullEntry.SystemProperties.Version);

            _ = await ContentfulManagementClient.PublishEntry(fullEntry.SystemProperties.Id,
                    fullEntry.SystemProperties.Version!.Value + 1);
        }
    }

    private static object? GetPropertyValue(ExpandoObject obj, params string[] path)
    {
        if (obj is null) return null;

        if (path.Length == 0) return null;

        var dict = (IDictionary<string, object?>)obj;

        if (!dict.TryGetValue(path[0], out var value)) return null;

        if (value == null) return null;

        if (path.Length > 1 && value is ExpandoObject expando)
        {
            return GetPropertyValue(expando, path[1..]);
        }

        return value;
    }

    private static string ReplaceFields(string prompt, ExpandoObject entry)
    {
        var template = Template.Parse(prompt);

        var result = template.Render(new { entry }, member => member.Name);

        return result;
    }
}