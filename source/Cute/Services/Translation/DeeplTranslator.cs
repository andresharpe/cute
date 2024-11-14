using Cute.Config;
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
            if (!appSettings.GetSettings().TryGetValue("Cute__DeeplApiKey", out var deeplApiKey))
            {
                throw new CliException("Deepl API Key not found in appsettings.json");
            }
            _translator = new Translator("deeplApiKey");
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

        public async Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode)
        {
            var result = await _translator.TranslateTextAsync(textToTranslate, fromLanguageCode, toLanguageCode);
            return new TranslationResponse
            {
                Text = result.Text,
                TargetLanguage = toLanguageCode
            };
        }
    }
}
