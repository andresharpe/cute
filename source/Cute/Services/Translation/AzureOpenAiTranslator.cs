using Azure.AI.OpenAI;
using Cute.Config;
using Cute.Lib.AiModels;
using Cute.Lib.AzureOpenAi;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Services.Translation.Interfaces;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;

namespace Cute.Services.Translation
{
    public class AzureOpenAiTranslator : ITranslator
    {
        private readonly ChatCompletionOptions _defaultChatCompletionOptions = new ChatCompletionOptions()
        {
            MaxOutputTokenCount = 4096,
            Temperature = 0.2f,
            FrequencyPenalty = 0.1f,
            PresencePenalty = 0.1f,
            TopP = 0.85f
        };

        private readonly ChatCompletionOptions _thresholdChatCompletionOptions = new ChatCompletionOptions();
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
            TranslationResponse result = new TranslationResponse
            {
                Text = await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, toLanguageCode, null, cuteContentTypeTranslation?.TranslationContext, glossary),
                TargetLanguage = toLanguageCode
            };

            return result;
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes, CuteContentTypeTranslation? cuteContentTypeTranslation)
        {
            var results = new List<TranslationResponse>();
            foreach (var languageCode in toLanguageCodes)
            {
                var translation = await Translate(textToTranslate, fromLanguageCode, languageCode, cuteContentTypeTranslation);
                results.Add(translation!);
            }

            return results.ToArray();
        }

        public async Task<TranslationResponse[]?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, IEnumerable<CuteLanguage> toLanguages)
        {
            List<TranslationResponse> results = new();
            foreach (var toLanguage in toLanguages)
            {
                var translation = await TranslateWithCustomModel(textToTranslate, fromLanguageCode, toLanguage, null);
                results.Add(translation!);
            }

            return results.ToArray();
        }

        public async Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, Dictionary<string, string>? glossary = null)
        {
            return await TranslateWithCustomModel(textToTranslate, fromLanguageCode, toLanguage, null); 
        }

        public async Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, CuteContentTypeTranslation? cuteContentTypeTranslation, Dictionary<string, string>? glossary = null)
        {
            TranslationResponse result = new TranslationResponse
            {
                Text = await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, toLanguage.Iso2Code, toLanguage.TranslationContext, cuteContentTypeTranslation?.TranslationContext, glossary, toLanguage.SymbolCountThreshold, toLanguage.ThresholdSetting),
                TargetLanguage = toLanguage.Iso2Code
            };

            return result;
        }

        private async Task<string> GeneratePromptAndTranslate(string textToTranslate, string fromLanguageCode, string toLanguageCode, string? languagePrompt, string? contentTypePrompt, Dictionary<string, string>? glossary, int? symbolCountThreshold = null, string? thresholdSetting = null)
        {
            (var chatClient, var chatCompletionOptions) = GetChatClient(textToTranslate, symbolCountThreshold, thresholdSetting);

            List<ChatMessage> messages = [];
            var systemMessageText = $"{languagePrompt} {contentTypePrompt}";
            if (!string.IsNullOrEmpty(systemMessageText.Trim()))
            {
                messages.Add(new SystemChatMessage(systemMessageText));
            }

            if (glossary != null && glossary.Count > 0)
            {
                messages.Add(new SystemChatMessage($"Consider the following glossary ({fromLanguageCode}:{toLanguageCode}) when translating:\n{string.Join('\n', glossary.Select(x => $"{x.Key} : {x.Value}"))}"));
            }

            messages.Add(new UserChatMessage($"Translate text from language {fromLanguageCode} to language {toLanguageCode}. Text: {textToTranslate}"));

            StringBuilder sb = new();
            await foreach (var part in chatClient.CompleteChatStreamingAsync(messages, chatCompletionOptions))
            {
                if (part == null || part.ToString() == null) continue;

                foreach (var token in part.ContentUpdate)
                {
                    sb.Append(token.Text);
                }
            }

            return sb.ToString();
        }

        private (ChatClient, ChatCompletionOptions) GetChatClient(string textToTranslate, int? symbolCountThreshold, string? thresholdSetting)
        {
            if(symbolCountThreshold.HasValue && !string.IsNullOrEmpty(thresholdSetting) && textToTranslate.Length < symbolCountThreshold)
            {
                return (_azureOpenAIClient.GetChatClient(thresholdSetting), _thresholdChatCompletionOptions);
            }

            return (_chatClient, _defaultChatCompletionOptions);
        }
    }
}
