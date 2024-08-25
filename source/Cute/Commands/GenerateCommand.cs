using Contentful.Core;
using Contentful.Core.Models;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.CommandRunners;
using Cute.Lib.Contentful;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Services;
using Cute.UiComponents;
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

public sealed class GenerateCommand : LoggedInCommand<GenerateCommand.Settings>
{
    private readonly ILogger<GenerateCommand> _logger;
    private readonly AzureTranslator _translator;
    private readonly GenerateCommandRunner _generateCommandRunner;

    public GenerateCommand(IConsoleWriter console, ILogger<GenerateCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings, AzureTranslator translator,
        GenerateCommandRunner generateCommandRunner)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _logger = logger;
        _translator = translator;
        _generateCommandRunner = generateCommandRunner;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-i|--prompt-id")]
        [Description("The id of the Contentful prompt entry to generate prompts from.")]
        public string PromptId { get; set; } = default!;

        [CommandOption("-e|--entry-id")]
        [Description("The entry id to process.")]
        public string? EntryId { get; set; } = null;

        [CommandOption("-r|--related-entry-id")]
        [Description("The related entry id to process.")]
        public string? RelatedEntryId { get; set; } = null;

        [CommandOption("--use-azure-translator")]
        [Description("Use Azure Translator service, otherwise let the LLM handle translations.")]
        public bool UseAzureTranslator { get; set; } = false;

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
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        var allLocaleCodes = Locales.Select(locales => locales.Code).ToHashSet();

        var displayActions = new CommandRunnerDisplayActions()
        {
            DisplayNormal = _console.WriteNormal,
            DisplayFormatted = f => _console.WriteNormalWithHighlights(f, Globals.StyleHeading),
            DisplayAlert = _console.WriteAlert,
            DisplayDim = _console.WriteDim,
            DisplayHeading = _console.WriteHeading,
            DisplayRuler = _console.WriteRuler,
            DisplayBlankLine = _console.WriteBlankLine,
        };

        await ProgressBars.Instance().StartAsync(async ctx =>
        {
            var taskGenerate = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.Robot}  Generating[/]");

            var runnerResult = await _generateCommandRunner.GenerateContent(settings.PromptId,
                displayActions,
                (step, steps) =>
                {
                    taskGenerate.MaxValue = steps;
                    taskGenerate.Value = step;
                }
            );

            if (runnerResult.Result == RunnerResult.Error)
            {
                throw new CliException(runnerResult.Message);
            }

            taskGenerate.StopTask();
        });

        return 0;

        /*

        // Generator language settings

        var generatorLanguageEntry = (JObject)(await ContentfulManagementClient.GetEntry(generatorLanguage.Sys.Id)).Fields;
        var glEntry = new Entry<JObject> { Fields = generatorLanguageEntry };
        var generatorLanguageCode = GetString(glEntry, "iso2code") ?? DefaultLocaleCode;
        var generatorLanguageName = GetString(glEntry, "name");
        if (!allLocaleCodes.Contains(generatorLanguageCode) || generatorLanguageName is null)
        {
            throw new CliException($"Generator locale '{generatorLanguageCode}' does not exist in your Contentful space.");
        }

        // Translator language settings

        Dictionary<string, string> translatedLanguageCodeAndName = [];
        foreach (var language in translatorLanguages)
        {
            var translatorLanguageEntry = (JObject)(await ContentfulManagementClient.GetEntry(language.Sys.Id)).Fields;
            var tlEntry = new Entry<JObject> { Fields = translatorLanguageEntry };
            var translatorLanguageCode = GetString(tlEntry, "iso2code") ?? DefaultLocaleCode;
            var translatorLanguageName = GetString(tlEntry, "name");
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

            if (settings.UseAzureTranslator)
            {
                await TranslateContentWithAzure(
                    promptContentFieldId,
                    contentType,
                    generatorLanguageCode,
                    translatedLanguageCodeAndName,
                    fullEntry,
                    fieldValue);
            }
            else
            {
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
            }

            if (++limit >= settings.Limit)
            {
                break;
            }
        }

        return 0;
        */
    }

    private async Task GenerateContent(string promptContentFieldId, string promptSystemMessage,
        string promptMainPrompt, ContentType contentType, string generatorLanguageCode,
        ChatClient chatClient, ChatCompletionOptions generatorCompletionOptions, ExpandoObject entry,
        int delay)
    {
        _console.WriteBlankLine();

        _console.WriteHeading("{displayField}", GetPropertyValue(entry, contentType.DisplayField)?.ToString());

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

    private async Task TranslateContentWithAzure(string promptContentFieldId, ContentType contentType, string generatorLanguageCode,
        Dictionary<string, string> translatorLanguageCodeAndName,
        Entry<JObject> fullEntry, string quotedText)
    {
        var codesToTranslate = new HashSet<string>();

        foreach (var (languageCode, _) in translatorLanguageCodeAndName)
        {
            if (!string.IsNullOrEmpty(GetString(fullEntry, promptContentFieldId, languageCode)))
            {
                continue;
            }
            codesToTranslate.Add(languageCode);
        }

        if (codesToTranslate.Count == 0) return;

        _console.WriteBlankLine();
        _console.WriteHeading("{displayField} > {language}", GetString(fullEntry, contentType.DisplayField),
            string.Join(", ", codesToTranslate.Select(c => translatorLanguageCodeAndName[c])));
        _console.WriteBlankLine();
        _console.WriteDim(quotedText);

        var translations = await _translator.Translate(generatorLanguageCode, codesToTranslate, quotedText);

        if (translations is null) return;

        var updated = false;

        foreach (var translation in translations)
        {
            var languageCode = translation.To;
            var output = translation.Text;

            AnsiConsole.Write(new Rule() { Style = Globals.StyleDim });
            _console.WriteHeading($"{GetString(fullEntry, contentType.DisplayField)} > {translatorLanguageCodeAndName[languageCode]}");
            _console.WriteNormal(output);

            var fieldDict = fullEntry.Fields;

            if (fieldDict[promptContentFieldId] == null)
            {
                fieldDict[promptContentFieldId] = new JObject(new JProperty(languageCode, output));
            }
            else if (fieldDict[promptContentFieldId] is JObject existingValues)
            {
                if (existingValues[languageCode] == null)
                {
                    existingValues.Add(new JProperty(languageCode, output));
                }
                else
                {
                    existingValues[languageCode] = output;
                }
            }

            _console.WriteBlankLine();

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
            if (!string.IsNullOrEmpty(GetString(fullEntry, promptContentFieldId, languageCode)))
            {
                continue;
            }

            var prompt = promptTemplate.Replace("{{ languageName }}", languageName);

            _console.WriteBlankLine();

            _console.WriteHeading("{displayField} > {language}", GetString(fullEntry, contentType.DisplayField), languageName);

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