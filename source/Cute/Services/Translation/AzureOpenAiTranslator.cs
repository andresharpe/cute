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
            var results = await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, new[] { toLanguageCode }, null, cuteContentTypeTranslation?.TranslationContext, glossary);
            return results?.FirstOrDefault();
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes, CuteContentTypeTranslation? cuteContentTypeTranslation)
        {
            return await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, toLanguageCodes, null, cuteContentTypeTranslation?.TranslationContext, null);
        }

        public async Task<TranslationResponse[]?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, IEnumerable<CuteLanguage> toLanguages)
        {
            var firstLanguage = toLanguages.FirstOrDefault();
            if (firstLanguage == null) return Array.Empty<TranslationResponse>();
            
            var languageCodes = toLanguages.Select(l => l.Iso2Code);
            return await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, languageCodes, firstLanguage.TranslationContext, null, null, firstLanguage.SymbolCountThreshold, firstLanguage.ThresholdSetting);
        }

        public async Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, Dictionary<string, string>? glossary = null)
        {
            return await TranslateWithCustomModel(textToTranslate, fromLanguageCode, toLanguage, null); 
        }

        public async Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, CuteContentTypeTranslation? cuteContentTypeTranslation, Dictionary<string, string>? glossary = null)
        {
            var results = await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, new[] { toLanguage.Iso2Code }, toLanguage.TranslationContext, cuteContentTypeTranslation?.TranslationContext, glossary, toLanguage.SymbolCountThreshold, toLanguage.ThresholdSetting);
            return results?.FirstOrDefault();
        }

        private async Task<TranslationResponse[]?> GeneratePromptAndTranslate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes, string? languagePrompt, string? contentTypePrompt, Dictionary<string, string>? glossary, int? symbolCountThreshold = null, string? thresholdSetting = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var symbolCount = textToTranslate.Length;
            var toLanguageCodesArray = toLanguageCodes.ToArray();
            var targetLanguagesStr = string.Join(", ", toLanguageCodesArray);
            
            (var chatClient, var chatCompletionOptions) = GetChatClient(textToTranslate, symbolCountThreshold, thresholdSetting, toLanguageCodesArray.Length);

            List<ChatMessage> messages = [];
            
            // Add strict JSON-only instruction
            messages.Add(new SystemChatMessage("You are a translation API that ONLY outputs valid JSON. Never include explanations, markdown, or any text outside the JSON object."));
            
            var systemMessageText = $"{languagePrompt} {contentTypePrompt}";
            if (!string.IsNullOrEmpty(systemMessageText.Trim()))
            {
                messages.Add(new SystemChatMessage(systemMessageText));
            }

            if (glossary != null && glossary.Count > 0)
            {
                messages.Add(new SystemChatMessage($"Consider the following glossary ({fromLanguageCode}:{targetLanguagesStr}) when translating:\n{string.Join('\n', glossary.Select(x => $"{x.Key} : {x.Value}"))}"));
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

            stopwatch.Stop();
            
            var jsonResponse = sb.ToString();
            var results = ParseTranslationResponse(jsonResponse, toLanguageCodesArray);
            
            await LogBenchmark(symbolCount, fromLanguageCode, targetLanguagesStr, stopwatch.ElapsedMilliseconds);
            
            return results;
        }

        private (ChatClient, ChatCompletionOptions) GetChatClient(string textToTranslate, int? symbolCountThreshold, string? thresholdSetting, int languageCount = 1)
        {
            ChatCompletionOptions options;
            ChatClient client;
            
            if(symbolCountThreshold.HasValue && !string.IsNullOrEmpty(thresholdSetting) && textToTranslate.Length < symbolCountThreshold)
            {
                client = _azureOpenAIClient.GetChatClient(thresholdSetting);
                options = _thresholdChatCompletionOptions;
            }
            else
            {
                client = _chatClient;
                options = _defaultChatCompletionOptions;

                // For multi-language translations, increase max output tokens
                if (languageCount > 1)
                {
                    // Create a new options object with increased token limit
                    // Estimate: base response + (language count * text length factor)
                    var estimatedTokens = Math.Min(16000, 1000 + (languageCount * textToTranslate.Length * 2));
                    options = new ChatCompletionOptions()
                    {
                        MaxOutputTokenCount = estimatedTokens,
                        Temperature = options.Temperature ?? 0.2f,
                        FrequencyPenalty = options.FrequencyPenalty ?? 0.1f,
                        PresencePenalty = options.PresencePenalty ?? 0.1f,
                        TopP = options.TopP ?? 0.85f,
                        ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() // Force JSON output
                    };
                }
                else
                {
                    // Also use JSON format for single language to be consistent
                    options = new ChatCompletionOptions()
                    {
                        MaxOutputTokenCount = options.MaxOutputTokenCount,
                        Temperature = options.Temperature ?? 0.2f,
                        FrequencyPenalty = options.FrequencyPenalty ?? 0.1f,
                        PresencePenalty = options.PresencePenalty ?? 0.1f,
                        TopP = options.TopP ?? 0.85f,
                        ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() // Force JSON output
                    };
                }
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
                        var results = new List<TranslationResponse>();
                        foreach (var targetLanguage in targetLanguages)
                        {
                            if (translations.TryGetValue(targetLanguage, out var translatedText))
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
                
                // Fallback: if JSON parsing fails, return the entire response for the first language
                return new[] { new TranslationResponse { Text = jsonResponse, TargetLanguage = targetLanguages.FirstOrDefault() ?? "unknown" } };
            }
            catch (Exception)
            {
                // If parsing fails completely, return the raw response
                return new[] { new TranslationResponse { Text = jsonResponse, TargetLanguage = targetLanguages.FirstOrDefault() ?? "unknown" } };
            }
        }

        private static readonly SemaphoreSlim _logSemaphore = new SemaphoreSlim(1, 1);
        private const string LogFilePath = "PromptBenchmark.txt";

        private async Task LogBenchmark(int symbolCount, string source, string target, long elapsedMilliseconds)
        {
            await _logSemaphore.WaitAsync();
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"{timestamp} > Translated {symbolCount} from {source} to {target} in {elapsedMilliseconds}ms{Environment.NewLine}";
                await File.AppendAllTextAsync(LogFilePath, logEntry);
            }
            finally
            {
                _logSemaphore.Release();
            }
        }
    }
}
