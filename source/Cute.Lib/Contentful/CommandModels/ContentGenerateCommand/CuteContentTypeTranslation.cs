namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand
{
    public class CuteContentTypeTranslation
    {
        public string Key { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string ContentType { get; set; } = default!;
        public string TranslationContext { get; set; } = default!;
    }
}
