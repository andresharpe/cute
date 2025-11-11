namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand
{
    public class CuteLanguage
    {
        public string Key { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string NativeName { get; set; } = default!;
        public string Iso2Code { get; set; } = default!;
        public string WikidataQid { get; set; } = default!;
        public bool IsContentfulLocale { get; set; } = default!;
        public string TranslationService { get; set; } = default!;
        public string TranslationContext { get; set; } = default!;
        public int? SymbolCountThreshold { get; set; } = default!;
        public string? ThresholdSetting { get; set; } = default!;
    }
}
