using Azure.AI.OpenAI;
using Cute.Config;
using Cute.Lib.AiModels;
using Cute.Lib.AzureOpenAi;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Services.Translation.Interfaces;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;
using System.Text.Json;

namespace Cute.Services.Translation
{
    public class AzureOpenAiTranslator : ITranslator
    {
        private const int DEFAULT_TIMEOUT_SECONDS = 120;
        private const int MULTI_LANGUAGE_TIMEOUT_SECONDS = 300; // 5 minutes for multi-language
        private const int DEFAULT_MAX_OUTPUT_TOKEN_COUNT = 4096;
        private const int RESPONSE_EXCERPT_LENGTH = 300;
        private const int MAX_CONSECUTIVE_TRUNCATIONS = 2;

        private readonly ChatClient _chatClient;
        private readonly IConsoleWriter _console;

        private readonly AzureOpenAiOptions _azureOpenAiOptions;
        private readonly AzureOpenAIClient _azureOpenAIClient;

        /// <summary>
        /// When true the default (non-threshold) completion options are sent without a MaxOutputTokenCount,
        /// letting the model use its full output budget. Large source texts (roughly 16k characters and up)
        /// cannot be translated within the default <see cref="DEFAULT_MAX_OUTPUT_TOKEN_COUNT"/> token cap and
        /// come back truncated, which makes the JSON unparseable and silently loses the translation.
        /// Set from the '--no-max-token-count' option on the translate command.
        /// </summary>
        public bool NoMaxTokenCount { get; set; } = false;

        public AzureOpenAiTranslator(IAzureOpenAiOptionsProvider azureOpenAiOptionsProvider, AppSettings appSettings, IConsoleWriter console)
        {
            _azureOpenAiOptions = azureOpenAiOptionsProvider.GetAzureOpenAIClientOptions();
            _console = console;

            _azureOpenAIClient = new(
                new Uri(_azureOpenAiOptions.Endpoint),
                new ApiKeyCredential(_azureOpenAiOptions.ApiKey)
            );
            _chatClient = _azureOpenAIClient.GetChatClient(_azureOpenAiOptions.DeploymentName);
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes)
        {
            return await Translate(textToTranslate, fromLanguageCode, toLanguageCodes, null);
        }

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode, Dictionary<string, string>? glossary = null)
        {
            return await Translate(textToTranslate, fromLanguageCode, toLanguageCode, null, glossary);
        }

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode, CuteContentTypeTranslation? cuteContentTypeTranslation, Dictionary<string, string>? glossary = null)
        {
            // Single glossary for single language
            var glossaries = glossary != null ? new Dictionary<string, Dictionary<string, string>> { { toLanguageCode, glossary } } : null;
            var results = await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, new[] { toLanguageCode }, null, cuteContentTypeTranslation?.TranslationContext, glossaries);
            return results?.FirstOrDefault();
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes, CuteContentTypeTranslation? cuteContentTypeTranslation)
        {
            return await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, toLanguageCodes, null, cuteContentTypeTranslation?.TranslationContext, null);
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<CuteLanguage> toLanguages, Dictionary<string, Dictionary<string, string>>? glossaries = null)
        {
            var firstLanguage = toLanguages.FirstOrDefault();
            if (firstLanguage == null) return Array.Empty<TranslationResponse>();

            var languageCodes = toLanguages.Select(l => l.Iso2Code);
            return await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, languageCodes, null, null, glossaries, firstLanguage.SymbolCountThreshold, firstLanguage.ThresholdSetting);
        }

        public async Task<TranslationResponse[]?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, IEnumerable<CuteLanguage> toLanguages, Dictionary<string, Dictionary<string, string>>? glossaries = null)
        {
            var firstLanguage = toLanguages.FirstOrDefault();
            if (firstLanguage == null) return Array.Empty<TranslationResponse>();

            var languageCodes = toLanguages.Select(l => l.Iso2Code);
            return await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, languageCodes, firstLanguage.TranslationContext, null, glossaries, firstLanguage.SymbolCountThreshold, firstLanguage.ThresholdSetting);
        }

        public async Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, Dictionary<string, string>? glossary = null)
        {
            return await TranslateWithCustomModel(textToTranslate, fromLanguageCode, toLanguage, null, glossary);
        }

        public async Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, CuteContentTypeTranslation? cuteContentTypeTranslation, Dictionary<string, string>? glossary = null)
        {
            // Single glossary for single language
            var glossaries = glossary != null ? new Dictionary<string, Dictionary<string, string>> { { toLanguage.Iso2Code, glossary } } : null;
            var results = await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, new[] { toLanguage.Iso2Code }, toLanguage.TranslationContext, cuteContentTypeTranslation?.TranslationContext, glossaries, toLanguage.SymbolCountThreshold, toLanguage.ThresholdSetting);
            return results?.FirstOrDefault();
        }

        private async Task<TranslationResponse[]?> GeneratePromptAndTranslate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes, string? languagePrompt, string? contentTypePrompt, Dictionary<string, Dictionary<string, string>>? glossaries, int? symbolCountThreshold = null, string? thresholdSetting = null)
        {
            var symbolCount = textToTranslate.Length;
            var toLanguageCodesArray = toLanguageCodes.ToArray();
            var targetLanguagesStr = string.Join(", ", toLanguageCodesArray);

            // Check if we should translate one-by-one: using threshold model (GPT-4o) with multiple languages
            // When text >= symbolCountThreshold, we use GPT-4o which has limited output tokens, so translate one by one
            var isSubThresholdModel = symbolCountThreshold.HasValue && !string.IsNullOrEmpty(thresholdSetting) && textToTranslate.Length <= symbolCountThreshold;
            var shouldTranslateOneByOne = !isSubThresholdModel && toLanguageCodesArray.Length > 1;

            if (shouldTranslateOneByOne)
            {
                // TODO: Revisit this to refactor
                // Translate each language separately to avoid output token limits with GPT-4o
                return await TranslateOneByOne(textToTranslate, fromLanguageCode, toLanguageCodesArray, languagePrompt, contentTypePrompt, glossaries, symbolCountThreshold, thresholdSetting);
            }

            (var chatClient, var chatCompletionOptions) = GetChatClient(textToTranslate, symbolCountThreshold, thresholdSetting, toLanguageCodesArray.Length);

            List<ChatMessage> messages = [];

            // Add strict JSON-only instruction
            messages.Add(new SystemChatMessage("You are a translation API that ONLY outputs valid JSON. Never include explanations, markdown, or any text outside the JSON object."));

            var systemMessageText = $"{languagePrompt} {contentTypePrompt}";
            if (!string.IsNullOrEmpty(systemMessageText.Trim()))
            {
                messages.Add(new SystemChatMessage(systemMessageText));
            }

            // Add glossaries for all target languages
            if (glossaries != null && glossaries.Count > 0)
            {
                var glossaryText = new StringBuilder();
                foreach (var targetLang in toLanguageCodesArray)
                {
                    if (glossaries.TryGetValue(targetLang, out var glossary) && glossary.Count > 0)
                    {
                        glossaryText.AppendLine($"\nGlossary for {fromLanguageCode} -> {targetLang}:");
                        foreach (var term in glossary)
                        {
                            glossaryText.AppendLine($"  {term.Key} : {term.Value}");
                        }
                    }
                }

                if (glossaryText.Length > 0)
                {
                    messages.Add(new SystemChatMessage($"Consider the following glossaries when translating:{glossaryText}"));
                }
            }

            // Create a more strict prompt that emphasizes JSON-only output
            var userPrompt = $@"Translate the text from {fromLanguageCode} to these languages: {targetLanguagesStr}.

IMPORTANT: Return ONLY a valid JSON object with no additional text, explanation, or markdown.
Format: {{""locale"":""translation""}}

Text to translate:
{textToTranslate}";

            messages.Add(new UserChatMessage(userPrompt));

            // Calculate timeout based on number of languages and text length
            var timeoutSeconds = toLanguageCodesArray.Length > 1 ? MULTI_LANGUAGE_TIMEOUT_SECONDS : DEFAULT_TIMEOUT_SECONDS;

            string jsonResponse;
            ChatFinishReason? finishReason;

            try
            {
                (jsonResponse, finishReason) = await StreamChatAsync(chatClient, messages, chatCompletionOptions, timeoutSeconds);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Translation request timed out after {timeoutSeconds} seconds for {toLanguageCodesArray.Length} language(s) with {symbolCount} characters.");
            }

            // Parse first. A response that ran past the cap after closing its JSON object still parses,
            // so judge the truncation on what could actually be read rather than discarding it unseen.
            var results = ParseTranslationResponse(jsonResponse, toLanguageCodesArray);

            HandleTruncation(finishReason, toLanguageCodesArray, symbolCount, chatCompletionOptions, jsonResponse, results);

            return results;
        }

        private async Task<TranslationResponse[]?> TranslateOneByOne(string textToTranslate, string fromLanguageCode, string[] toLanguageCodes, string? languagePrompt, string? contentTypePrompt, Dictionary<string, Dictionary<string, string>>? glossaries, int? symbolCountThreshold, string? thresholdSetting)
        {
            var results = new List<TranslationResponse>();
            var consecutiveTruncations = 0;

            for (var i = 0; i < toLanguageCodes.Length; i++)
            {
                var targetLanguage = toLanguageCodes[i];

                // Extract glossary for this specific language
                Dictionary<string, string>? glossary = null;
                if (glossaries != null && glossaries.TryGetValue(targetLanguage, out var langGlossary))
                {
                    glossary = langGlossary;
                }

                try
                {
                    var singleResult = await TranslateSingleLanguage(textToTranslate, fromLanguageCode, targetLanguage, languagePrompt, contentTypePrompt, glossary, symbolCountThreshold, thresholdSetting);

                    if (singleResult != null)
                    {
                        results.Add(singleResult);
                    }

                    consecutiveTruncations = 0;
                }
                catch (TranslationTruncatedException ex)
                {
                    consecutiveTruncations++;

                    _console.WriteAlert($"[{targetLanguage}] {ex.Message}");

                    // How much output a translation needs varies by language, so one truncation does not
                    // condemn the rest. Repeated truncations mean the source text itself is too long - stop
                    // there rather than spending a request per remaining locale to prove the same point.
                    if (consecutiveTruncations >= MAX_CONSECUTIVE_TRUNCATIONS)
                    {
                        var remaining = toLanguageCodes.Length - i - 1;
                        if (remaining > 0)
                        {
                            _console.WriteAlert($"Skipping the remaining {remaining} language(s) for this text after {consecutiveTruncations} truncated responses in a row.");
                        }

                        break;
                    }
                }
            }

            return results.ToArray();
        }

        private async Task<TranslationResponse?> TranslateSingleLanguage(string textToTranslate, string fromLanguageCode, string toLanguageCode, string? languagePrompt, string? contentTypePrompt, Dictionary<string, string>? glossary, int? symbolCountThreshold, string? thresholdSetting)
        {
            (var chatClient, var chatCompletionOptions) = GetChatClient(textToTranslate, symbolCountThreshold, thresholdSetting, 1);

            List<ChatMessage> messages = [];
            messages.Add(new SystemChatMessage("You are a translation API that ONLY outputs valid JSON. Never include explanations, markdown, or any text outside the JSON object."));

            var systemMessageText = $"{languagePrompt} {contentTypePrompt}";
            if (!string.IsNullOrEmpty(systemMessageText.Trim()))
            {
                messages.Add(new SystemChatMessage(systemMessageText));
            }

            if (glossary != null && glossary.Count > 0)
            {
                messages.Add(new SystemChatMessage($"Consider the following glossary ({fromLanguageCode}:{toLanguageCode}) when translating:\n{string.Join('\n', glossary.Select(x => $"{x.Key} : {x.Value}"))}"));
            }

            var userPrompt = $@"Translate the text from {fromLanguageCode} to {toLanguageCode}.

IMPORTANT: Return ONLY a valid JSON object with no additional text.
Format: {{""{toLanguageCode}"":""translation""}}

Text to translate:
{textToTranslate}";

            messages.Add(new UserChatMessage(userPrompt));

            string jsonResponse;
            ChatFinishReason? finishReason;

            try
            {
                (jsonResponse, finishReason) = await StreamChatAsync(chatClient, messages, chatCompletionOptions, DEFAULT_TIMEOUT_SECONDS);
            }
            catch (OperationCanceledException)
            {
                _console.WriteAlert($"Translation to '{toLanguageCode}' timed out after {DEFAULT_TIMEOUT_SECONDS} seconds for {textToTranslate.Length} characters. No translation was written for this locale.");
                return null;
            }

            var parsedResult = ParseTranslationResponse(jsonResponse, [toLanguageCode]);

            HandleTruncation(finishReason, [toLanguageCode], textToTranslate.Length, chatCompletionOptions, jsonResponse, parsedResult);

            return parsedResult?.Length > 0 ? parsedResult[0] : null;
        }

        private static async Task<(string Content, ChatFinishReason? FinishReason)> StreamChatAsync(
            ChatClient chatClient,
            List<ChatMessage> messages,
            ChatCompletionOptions chatCompletionOptions,
            int timeoutSeconds)
        {
            var sb = new StringBuilder();
            ChatFinishReason? finishReason = null;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            await foreach (var part in chatClient.CompleteChatStreamingAsync(messages, chatCompletionOptions).WithCancellation(cts.Token))
            {
                if (part == null) continue;

                if (part.FinishReason.HasValue)
                {
                    finishReason = part.FinishReason;
                }

                foreach (var token in part.ContentUpdate)
                {
                    sb.Append(token.Text);
                }
            }

            return (sb.ToString(), finishReason);
        }

        /// <summary>
        /// A completion that stops because it ran out of output tokens returns a partial - and usually
        /// unparseable - JSON document. Previously that was swallowed by the parser and reported as
        /// "no translation", with no indication that the model had actually answered and been billed for it.
        /// Anything the parser could still salvage is kept; only a total loss is raised as an exception.
        /// </summary>
        private void HandleTruncation(ChatFinishReason? finishReason, string[] targetLanguages, int symbolCount, ChatCompletionOptions chatCompletionOptions, string response, TranslationResponse[]? salvagedResults)
        {
            if (finishReason is null || finishReason.Value != ChatFinishReason.Length) return;

            var limit = chatCompletionOptions.MaxOutputTokenCount.HasValue
                ? $"the configured output limit of {chatCompletionOptions.MaxOutputTokenCount} tokens"
                : "the model's maximum output length";

            var hint = chatCompletionOptions.MaxOutputTokenCount.HasValue
                ? " Re-run the translate command with '--no-max-token-count' to send the request without cute's output token cap, or split the source text into smaller fields."
                : " The source text is too long to translate in a single response - split it into smaller fields.";

            var message = $"The model stopped early (finish reason 'length') translating {symbolCount} characters to {string.Join(", ", targetLanguages)}. " +
                $"The response was cut off at {limit} after {response.Length} characters.";

            if (salvagedResults is { Length: > 0 })
            {
                if (salvagedResults.Length == targetLanguages.Length)
                {
                    // Everything asked for came back, so this is a warning about how close to the
                    // limit the request ran rather than a failure.
                    _console.WriteDim($"{message} All {targetLanguages.Length} language(s) were still readable and have been kept.");
                }
                else
                {
                    _console.WriteAlert($"{message} {salvagedResults.Length} of {targetLanguages.Length} language(s) were still readable and have been kept; the rest were lost.{hint}");
                }

                return;
            }

            // ParseTranslationResponse has already written the response excerpt at this point.
            throw new TranslationTruncatedException($"{message} Nothing usable could be read out of it.{hint}");
        }

        private (ChatClient, ChatCompletionOptions) GetChatClient(string textToTranslate, int? symbolCountThreshold, string? thresholdSetting, int languageCount = 1)
        {
            ChatCompletionOptions options;
            ChatClient client;

            if (symbolCountThreshold.HasValue && !string.IsNullOrEmpty(thresholdSetting) && textToTranslate.Length <= symbolCountThreshold)
            {
                client = _azureOpenAIClient.GetChatClient(thresholdSetting);
                options = CreateThresholdChatCompletionOptions();
            }
            else
            {
                client = _chatClient;
                options = CreateDefaultChatCompletionOptions();
            }

            return (client, options);
        }

        private ChatCompletionOptions CreateDefaultChatCompletionOptions()
        {
            var options = new ChatCompletionOptions()
            {
                Temperature = 0.2f,
                FrequencyPenalty = 0.1f,
                PresencePenalty = 0.1f,
                TopP = 0.85f
            };

            if (!NoMaxTokenCount)
            {
                options.MaxOutputTokenCount = DEFAULT_MAX_OUTPUT_TOKEN_COUNT;
            }

            return options;
        }

        private static ChatCompletionOptions CreateThresholdChatCompletionOptions()
        {
            return new ChatCompletionOptions() { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() };
        }

        private TranslationResponse[]? ParseTranslationResponse(string jsonResponse, string[] targetLanguages)
        {
            var targets = string.Join(", ", targetLanguages);

            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                _console.WriteAlert($"The model returned an empty response when translating to {targets}. No translation was written for this locale.");
                return Array.Empty<TranslationResponse>();
            }

            // Try to extract JSON from the response (in case AI adds extra text)
            var jsonStart = jsonResponse.IndexOf('{');
            var jsonEnd = jsonResponse.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd <= jsonStart)
            {
                _console.WriteAlert($"The model's response for {targets} contains no complete JSON object - it was {jsonResponse.Length} characters and is most likely truncated. No translation was written for this locale.");
                _console.WriteDim($"Response: {Excerpt(jsonResponse)}");
                return Array.Empty<TranslationResponse>();
            }

            var jsonContent = jsonResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

            Dictionary<string, string>? translations;

            try
            {
                translations = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _console.WriteAlert($"The model's response for {targets} could not be parsed as JSON ({ex.Message}). It was {jsonResponse.Length} characters. No translation was written for this locale.");
                _console.WriteDim($"Response: {Excerpt(jsonResponse)}");
                return Array.Empty<TranslationResponse>();
            }

            if (translations == null)
            {
                _console.WriteAlert($"The model's response for {targets} deserialized to null. No translation was written for this locale.");
                _console.WriteDim($"Response: {Excerpt(jsonResponse)}");
                return Array.Empty<TranslationResponse>();
            }

            // Validate that we got translations for ALL expected languages. A locale with no key at all
            // and a locale whose key holds an empty string are different mistakes and are reported
            // separately - lumping them together produces the contradictory "no translation for 'de',
            // keys returned were 'de'".
            var results = new List<TranslationResponse>();
            var languagesWithNoKey = new List<string>();
            var languagesWithEmptyValue = new List<string>();

            foreach (var targetLanguage in targetLanguages)
            {
                if (!translations.TryGetValue(targetLanguage, out var translatedText))
                {
                    languagesWithNoKey.Add(targetLanguage);
                }
                else if (string.IsNullOrEmpty(translatedText))
                {
                    languagesWithEmptyValue.Add(targetLanguage);
                }
                else
                {
                    results.Add(new TranslationResponse
                    {
                        Text = translatedText,
                        TargetLanguage = targetLanguage
                    });
                }
            }

            if (languagesWithNoKey.Count > 0)
            {
                // Naming the keys that did come back is what identifies the mistake - a locale variant
                // ('de-DE' where 'de' was asked for), or a literal key such as 'locale' or 'translation'.
                _console.WriteAlert($"The model's response for {targets} contains no key for: {string.Join(", ", languagesWithNoKey)}. " +
                    $"Keys returned were: {(translations.Count == 0 ? "(none)" : string.Join(", ", translations.Keys))}.");
            }

            if (languagesWithEmptyValue.Count > 0)
            {
                _console.WriteAlert($"The model returned an empty translation for: {string.Join(", ", languagesWithEmptyValue)}. " +
                    $"No translation was written for {(languagesWithEmptyValue.Count == 1 ? "this locale" : "these locales")}.");
            }

            return results.ToArray();
        }

        private static string Excerpt(string text)
        {
            var flattened = text.Replace("\r", " ").Replace("\n", " ").Trim();

            if (flattened.Length <= RESPONSE_EXCERPT_LENGTH)
            {
                return flattened;
            }

            // Both ends matter: the start shows whether it is JSON at all, the end shows where it stopped.
            var half = RESPONSE_EXCERPT_LENGTH / 2;

            return $"{flattened.Substring(0, half)} ... [{flattened.Length - RESPONSE_EXCERPT_LENGTH} characters omitted] ... {flattened.Substring(flattened.Length - half)}";
        }
    }
}
