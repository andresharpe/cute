using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

namespace Cute.Services.Translation.Interfaces
{
    public interface ITranslator
    {
        Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes);
        Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes, CuteContentTypeTranslation? cuteContentTypeTranslation);
        Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode, Dictionary<string, string>? glossary = null);
        Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode, CuteContentTypeTranslation? cuteContentTypeTranslation, Dictionary<string, string>? glossary = null);
        Task<TranslationResponse[]?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, IEnumerable<CuteLanguage> toLanguages);

        Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, Dictionary<string, string>? glossary = null);
        Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, CuteContentTypeTranslation? cuteContentTypeTranslation, Dictionary<string, string>? glossary = null);
    }

    public class TranslationResponse
    {
        public string Text { get; set; } = default!;
        public string TargetLanguage { get; set; } = default!;
    }
}
