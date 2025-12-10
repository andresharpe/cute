using Contentful.Core.Models;
using Cute.Lib.SiteGen.Models;

namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public class CuteContentGenerate
{
    public SystemProperties Sys { get; set; } = default!;
    public string Key { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string SystemMessage { get; set; } = default!;
    public string Prompt { get; set; } = default!;
    public string DeploymentModel { get; set; } = default!;
    public int? MaxTokenLimit { get; set; } = default!;
    public double? Temperature { get; set; } = default!;
    public double? TopP { get; set; } = default!;
    public double? FrequencyPenalty { get; set; } = default!;
    public double? PresencePenalty { get; set; } = default!;
    public CuteDataQuery CuteDataQueryEntry { get; set; } = default!;
    public string PromptOutputContentField { get; set; } = default!;
    public string? Locale { get; set; }
    public List<DataLanguage> TranslatorTargetDataLanguageEntries { get; set; } = default!;
}

public class CuteContentGenerateLocalized
{
    public SystemProperties Sys { get; set; } = default!;
    public Dictionary<string, string> Key { get; set; } = default!;
    public Dictionary<string, string> Title { get; set; } = default!;
    public Dictionary<string, string> SystemMessage { get; set; } = default!;
    public Dictionary<string, string> Prompt { get; set; } = default!;
    public Dictionary<string, string> DeploymentModel { get; set; } = default!;
    public Dictionary<string, int?> MaxTokenLimit { get; set; } = default!;
    public Dictionary<string, double?> Temperature { get; set; } = default!;
    public Dictionary<string, double?> TopP { get; set; } = default!;
    public Dictionary<string, double?> FrequencyPenalty { get; set; } = default!;
    public Dictionary<string, double?> PresencePenalty { get; set; } = default!;
    public Dictionary<string, CuteDataQueryLocalized> CuteDataQueryEntry { get; set; } = default!;
    public Dictionary<string, string> PromptOutputContentField { get; set; } = default!;

    public CuteContentGenerate GetBasicEntry(string targetLocale, string defaultLocale)
    {
        return new CuteContentGenerate
        {
            Sys = Sys,
            Key = Key[defaultLocale],
            Title = Title[defaultLocale],
            SystemMessage = SystemMessage[targetLocale],
            Prompt = Prompt[targetLocale],
            DeploymentModel = DeploymentModel[defaultLocale],
            MaxTokenLimit = MaxTokenLimit != null && MaxTokenLimit.ContainsKey(defaultLocale) ? MaxTokenLimit[defaultLocale] : null,
            Temperature = Temperature != null && Temperature.ContainsKey(defaultLocale) ? Temperature[defaultLocale] : null,
            TopP = TopP != null && TopP.ContainsKey(defaultLocale) ? TopP[defaultLocale] : null,
            FrequencyPenalty = FrequencyPenalty != null && FrequencyPenalty.ContainsKey(defaultLocale) ? FrequencyPenalty[defaultLocale] : null,
            PresencePenalty = PresencePenalty != null && PresencePenalty.ContainsKey(defaultLocale) ? PresencePenalty[defaultLocale] : null,
            CuteDataQueryEntry = CuteDataQueryEntry[defaultLocale].GetBasicEntry(defaultLocale),
            PromptOutputContentField = PromptOutputContentField[defaultLocale],
            Locale = targetLocale
        };
    }
}