using Azure.AI.OpenAI;
using Cute.Lib.AiModels;
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

        public GPT4oTranslator(IAzureOpenAiOptionsProvider azureOpenAiOptionsProvider)
        {
            var options = azureOpenAiOptionsProvider.GetAzureOpenAIClientOptions();

            AzureOpenAIClient client = new(
                new Uri(options.Endpoint),
                new ApiKeyCredential(options.ApiKey)
            );

            _chatClient = client.GetChatClient(options.DeploymentName);
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes)
        {
            var results = new List<TranslationResponse>();
            foreach (var languageCode in toLanguageCodes)
            {
                var translation = await Translate(textToTranslate, fromLanguageCode, languageCode);
                results.Add(translation!);
            }

            return results.ToArray();
        }

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode)
        {
            TranslationResponse result = new TranslationResponse
            {
                Text = await GeneratePromptAndTranslate(textToTranslate, fromLanguageCode, toLanguageCode),
                TargetLanguage = toLanguageCode
            };

            return result;
        }

        public Task<TranslationResponse[]?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes)
        {
            throw new NotImplementedException();
        }

        public Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, string toLanguageCode)
        {
            throw new NotImplementedException();
        }

        private async Task<string> GeneratePromptAndTranslate(string textToTranslate, string fromLanguageCode, string toLanguageCode)
        {
            var message = new UserChatMessage($"Translate text from language {fromLanguageCode} to language {toLanguageCode}. Text: {textToTranslate}");

            StringBuilder sb = new();
            await foreach (var part in _chatClient.CompleteChatStreamingAsync([message], _chatCompletionOptions))
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
