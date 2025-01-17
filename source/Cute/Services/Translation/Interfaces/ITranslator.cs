using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

namespace Cute.Services.Translation.Interfaces
{
    public interface ITranslator
    {
        Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes);
        Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes, CuteContentTypeTranslation? cuteContentTypeTranslation);
        Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode);
        Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode, CuteContentTypeTranslation? cuteContentTypeTranslation);
        Task<TranslationResponse[]?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, IEnumerable<CuteLanguage> toLanguages);
        Task<TranslationResponse?> TranslateWithCustomModel(string textToTranslate, string fromLanguageCode, CuteLanguage toLanguage, CuteContentTypeTranslation? cuteContentTypeTranslation);
    }

    public class TranslationResponse
    {
        public string Text { get; set; } = default!;
        public string TargetLanguage { get; set; } = default!;
    }
}
