using Azure.AI.OpenAI;
using Cute.Config;
using Cute.Lib.AiModels;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Services.Translation.Interfaces;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;

namespace Cute.Services.Translation
{
    public class GPT4oTranslator : ITranslator
    {
        private readonly ChatCompletionOptions _chatCompletionOptions = new ChatCompletionOptions()
        {
            MaxOutputTokenCount = 4096,
            Temperature = 0.2f,
            FrequencyPenalty = 0.1f,
            PresencePenalty = 0.1f,
            TopP = 0.85f
        };
        private readonly ChatClient _chatClient;
        private readonly AppSettings _appSettings;

        public GPT4oTranslator(IAzureOpenAiOptionsProvider azureOpenAiOptionsProvider, AppSettings appSettings)
        {
            var options = azureOpenAiOptionsProvider.GetAzureOpenAIClientOptions();

            AzureOpenAIClient client = new(
                new Uri(options.Endpoint),
                new ApiKeyCredential(options.ApiKey)
            );

            _chatClient = client.GetChatClient(options.DeploymentName);
            _appSettings = appSettings;
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes)
        {
            return await Translate(textToTranslate, fromLanguageCode, toLanguageCodes, null);
        }

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode)
        {
            return await Translate(textToTranslate, fromLanguageCode, toLanguageCode, null);
        }

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode, CuteContentTypeTranslation? cuteContentTypeTranslation)
        {
            TranslationResponse result = new TranslationResponse
            {
                Text = await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, toLanguageCode, null, cuteContentTypeTranslation?.TranslationContext),
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

        public async Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage)
        {
            return await TranslateWithCustomModel(textToTranslate, fromLanguageCode, toLanguage, null); 
        }

        public async Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, CuteContentTypeTranslation? cuteContentTypeTranslation)
        {
            TranslationResponse result = new TranslationResponse
            {
                Text = await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, toLanguage.Iso2Code, toLanguage.TranslationContext, cuteContentTypeTranslation?.TranslationContext),
                TargetLanguage = toLanguage.Iso2Code
            };

            return result;
        }

        private async Task<string> GeneratePromptAndTranslate(string textToTranslate, string fromLanguageCode, string toLanguageCode, string? languagePrompt, string? contentTypePrompt)
        {
            List<ChatMessage> messages = [];
            var systemMessageText = $"{languagePrompt} {contentTypePrompt}";
            if (!string.IsNullOrEmpty(systemMessageText.Trim()))
            {
                messages.Add(new SystemChatMessage(systemMessageText));
            }

            messages.Add(new UserChatMessage($"Translate text from language {fromLanguageCode} to language {toLanguageCode}. Text: {textToTranslate}"));

            StringBuilder sb = new();
            await foreach (var part in _chatClient.CompleteChatStreamingAsync(messages, _chatCompletionOptions))
            {
                if (part == null || part.ToString() == null) continue;

                foreach (var token in part.ContentUpdate)
                {
                    sb.Append(token.Text);
                }
            }

            return sb.ToString();
        }
    }
}
