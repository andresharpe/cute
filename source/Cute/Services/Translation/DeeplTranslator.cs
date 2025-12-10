using Cute.Config;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Exceptions;
using Cute.Services.Translation.Interfaces;
using DeepL;
using ITranslator = Cute.Services.Translation.Interfaces.ITranslator;

namespace Cute.Services.Translation
{
    public class DeeplTranslator : ITranslator
    {
        Translator _translator;
        public DeeplTranslator(AppSettings appSettings)
        {
            if (!appSettings.GetSettings().TryGetValue("Cute__DeeplApiKey", out var deeplApiKey) || string.IsNullOrEmpty(deeplApiKey))
            {
                throw new CliException("Deepl API Key not found in the environment");
            }
            _translator = new Translator(deeplApiKey!);
        }
        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes)
        {
            List<TranslationResponse> results = new();
            foreach (var languageCode in toLanguageCodes)
            {
                var translation = await Translate(textToTranslate, fromLanguageCode, languageCode);
                results.Add(translation!);
            }

            return results.ToArray();
        }

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode, Dictionary<string, string>? glossary = null)
        {
            var result = await _translator.TranslateTextAsync(textToTranslate, fromLanguageCode, toLanguageCode);
            return new TranslationResponse
            {
                Text = result.Text,
                TargetLanguage = toLanguageCode
            };
        }

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode, CuteContentTypeTranslation? cuteContentTypeTranslation, Dictionary<string, string>? glossary = null)
        {
            return await Translate(textToTranslate, fromLanguageCode, toLanguageCode);
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes, CuteContentTypeTranslation? cuteContentTypeTranslation)
        {
            return await Translate(textToTranslate, fromLanguageCode, toLanguageCodes);
        }

        public async Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<CuteLanguage> toLanguages, Dictionary<string, Dictionary<string, string>>? glossaries = null)
        {
            return await Translate(textToTranslate, fromLanguageCode, toLanguages.Select(k => k.Iso2Code));
        }

        public Task<TranslationResponse[]?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, IEnumerable<CuteLanguage> toLanguages, Dictionary<string, Dictionary<string, string>>? glossaries = null)
        {
            throw new NotImplementedException();
        }

        public Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, Dictionary<string, string>? glossary = null)
        {
            throw new NotImplementedException();
        }

        public Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, CuteContentTypeTranslation? cuteContentTypeTranslation, Dictionary<string, string>? glossary = null)
        {
            throw new NotImplementedException();
        }
    }
}
