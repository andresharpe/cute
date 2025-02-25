using Cute.Config;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Exceptions;
using Cute.Services.Translation.Interfaces;
using Google.Cloud.Translation.V2;

namespace Cute.Services.Translation
{
    public class GoogleTranslator : ITranslator
    {
        private readonly TranslationClient _client;

        public GoogleTranslator(AppSettings appSettings)
        {
            if(!appSettings.GetSettings().TryGetValue("Cute__GoogleApiKey", out var googleApiKey))
            {
                throw new CliException("Google API Key not found in appsettings.json");
            }

            _client = TranslationClient.CreateFromApiKey(googleApiKey);
        }
        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes)
        {
            List<TranslationResponse> results = new();
            foreach (var toLanguageCode in toLanguageCodes)
            {
                var translation = await Translate(textToTranslate, fromLanguageCode, toLanguageCode);
                results.Add(translation!);
            }

            return results.ToArray();
        }

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode)
        {
            var result = await _client.TranslateTextAsync(textToTranslate, toLanguageCode, fromLanguageCode);
            return new TranslationResponse
            {
                TargetLanguage = toLanguageCode,
                Text = result.TranslatedText
            };
        }

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode, CuteContentTypeTranslation? cuteContentTypeTranslation)
        {
            return await Translate(textToTranslate, fromLanguageCode, toLanguageCode);
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes, CuteContentTypeTranslation? cuteContentTypeTranslation)
        {
            return await Translate(textToTranslate, fromLanguageCode, toLanguageCodes);
        }

        public Task<TranslationResponse[]?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, IEnumerable<CuteLanguage> toLanguages)
        {
            throw new NotImplementedException();
        }

        public Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage)
        {
            throw new NotImplementedException();
        }

        public Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, CuteContentTypeTranslation? cuteContentTypeTranslation)
        {
            throw new NotImplementedException();
        }
    }
}
