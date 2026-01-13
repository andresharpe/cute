using Azure.AI.OpenAI;
using Cute.Config;
using Cute.Lib.AiModels;
using Cute.Lib.AzureOpenAi;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Services.Translation.Interfaces;
using OpenAI.Chat;
using System.ClientModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Cute.Services.Translation
{
    public class AzureOpenAiTranslator : ITranslator
    {
        private const int DEFAULT_TIMEOUT_SECONDS = 120;
        private const int MULTI_LANGUAGE_TIMEOUT_SECONDS = 300; // 5 minutes for multi-language
        
        private readonly ChatCompletionOptions _defaultChatCompletionOptions = new ChatCompletionOptions()
        {
            MaxOutputTokenCount = 4096,
            Temperature = 0.2f,
            FrequencyPenalty = 0.1f,
            PresencePenalty = 0.1f,
            TopP = 0.85f
        };

        private readonly ChatCompletionOptions _thresholdChatCompletionOptions = new ChatCompletionOptions() { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() };
        private readonly ChatClient _chatClient;

        private readonly AzureOpenAiOptions _azureOpenAiOptions;
        private readonly AzureOpenAIClient _azureOpenAIClient;

        public AzureOpenAiTranslator(IAzureOpenAiOptionsProvider azureOpenAiOptionsProvider, AppSettings appSettings)
        {
            _azureOpenAiOptions = azureOpenAiOptionsProvider.GetAzureOpenAIClientOptions();

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
            var isUsingThresholdModel = symbolCountThreshold.HasValue && !string.IsNullOrEmpty(thresholdSetting) && textToTranslate.Length >= symbolCountThreshold;
            var shouldTranslateOneByOne = isUsingThresholdModel && toLanguageCodesArray.Length > 1;
            
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
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            
            StringBuilder sb = new();
            using var cts = new CancellationTokenSource(timeout);
            
            try
            {
                await foreach (var part in chatClient.CompleteChatStreamingAsync(messages, chatCompletionOptions).WithCancellation(cts.Token))
                {
                    if (part == null || part.ToString() == null) continue;

                    foreach (var token in part.ContentUpdate)
                    {
                        sb.Append(token.Text);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Translation request timed out after {timeoutSeconds} seconds for {toLanguageCodesArray.Length} language(s) with {symbolCount} characters.");
            }
                        
            var jsonResponse = sb.ToString();
            var results = ParseTranslationResponse(jsonResponse, toLanguageCodesArray);
            
            return results;
        }

        private async Task<TranslationResponse[]?> TranslateOneByOne(string textToTranslate, string fromLanguageCode, string[] toLanguageCodes, string? languagePrompt, string? contentTypePrompt, Dictionary<string, Dictionary<string, string>>? glossaries, int? symbolCountThreshold, string? thresholdSetting)
        {
            var results = new List<TranslationResponse>();
            
            foreach (var targetLanguage in toLanguageCodes)
            {
                // Extract glossary for this specific language
                Dictionary<string, string>? glossary = null;
                if (glossaries != null && glossaries.TryGetValue(targetLanguage, out var langGlossary))
                {
                    glossary = langGlossary;
                }
                
                var singleResult = await TranslateSingleLanguage(textToTranslate, fromLanguageCode, targetLanguage, languagePrompt, contentTypePrompt, glossary, symbolCountThreshold, thresholdSetting);
                
                if (singleResult != null)
                {
                    results.Add(singleResult);
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
            
            StringBuilder sb = new();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(DEFAULT_TIMEOUT_SECONDS));
            
            try
            {
                await foreach (var part in chatClient.CompleteChatStreamingAsync(messages, chatCompletionOptions).WithCancellation(cts.Token))
                {
                    if (part == null) continue;
                    foreach (var token in part.ContentUpdate)
                    {
                        sb.Append(token.Text);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            
            var jsonResponse = sb.ToString();
            var parsedResult = ParseTranslationResponse(jsonResponse, [toLanguageCode]);
            
            return parsedResult?.Length > 0 ? parsedResult[0] : null;
        }

        private (ChatClient, ChatCompletionOptions) GetChatClient(string textToTranslate, int? symbolCountThreshold, string? thresholdSetting, int languageCount = 1)
        {
            ChatCompletionOptions options;
            ChatClient client;
            
            if(symbolCountThreshold.HasValue && !string.IsNullOrEmpty(thresholdSetting) && textToTranslate.Length <= symbolCountThreshold)
            {
                client = _azureOpenAIClient.GetChatClient(thresholdSetting);
                options = _thresholdChatCompletionOptions;
            }
            else
            {
                client = _chatClient;
                options = _defaultChatCompletionOptions;
            }
            
            return (client, options);
        }
        
        private TranslationResponse[]? ParseTranslationResponse(string jsonResponse, string[] targetLanguages)
        {
            try
            {
                // Try to extract JSON from the response (in case AI adds extra text)
                var jsonStart = jsonResponse.IndexOf('{');
                var jsonEnd = jsonResponse.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonContent = jsonResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (translations != null)
                    {
                        // Validate that we got translations for ALL expected languages
                        var results = new List<TranslationResponse>();
                        foreach (var targetLanguage in targetLanguages)
                        {
                            if (translations.TryGetValue(targetLanguage, out var translatedText) && !string.IsNullOrEmpty(translatedText))
                            {
                                results.Add(new TranslationResponse
                                {
                                    Text = translatedText,
                                    TargetLanguage = targetLanguage
                                });
                            }
                        }
                        
                        return results.ToArray();
                    }
                }
                
                // Invalid JSON format or missing translations - return empty array
                return Array.Empty<TranslationResponse>();
            }
            catch (Exception)
            {
                // Parsing failed - return empty array
                return Array.Empty<TranslationResponse>();
            }
        }
    }
}
