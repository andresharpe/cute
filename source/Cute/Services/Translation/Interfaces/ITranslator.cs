namespace Cute.Services.Translation.Interfaces
{
    public interface ITranslator
    {
        Task<TranslationResponse[]?> Translate(string textToTranslate, string fromLanguageCode, IEnumerable<string> toLanguageCodes);
        Task<TranslationResponse?> Translate(string textToTranslate, string fromLanguageCode, string toLanguageCode);
    }

    public class TranslationResponse
    {
        public string Text { get; set; } = default!;
        public string TargetLanguage { get; set; } = default!;
    }
}
