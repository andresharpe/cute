using Cute.Lib.SiteGen.Models;

namespace Cute.Lib.CommandRunners.Models;

public class MetaPrompt
{
    public string Key { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string SystemMessage { get; set; } = default!;
    public string Prompt { get; set; } = default!;
    public double Temperature { get; set; } = default!;
    public double FrequencyPenalty { get; set; } = default!;
    public UiDataQuery UiDataQueryEntry { get; set; } = default!;
    public string PromptOutputContentField { get; set; } = default!;
    public DataLanguage GeneratorTargetLanguage { get; set; } = default!;
    public List<DataLanguage> TranslatorTargetLanguages { get; set; } = default!;
}