using Contentful.Core;
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
    public int MaxTokenLimit { get; set; } = default!;
    public double Temperature { get; set; } = default!;
    public double TopP { get; set; } = default!;
    public double FrequencyPenalty { get; set; } = default!;
    public double PresencePenalty { get; set; } = default!;
    public CuteDataQuery CuteDataQueryEntry { get; set; } = default!;
    public string PromptOutputContentField { get; set; } = default!;
    public DataLanguage GeneratorTargetDataLanguageEntry { get; set; } = default!;
    public List<DataLanguage> TranslatorTargetDataLanguageEntries { get; set; } = default!;

    public static CuteContentGenerate? GetByKey(ContentfulClient contentfulClient, string key)
    {
        return ContentfulEntryEnumerator
            .DeliveryEntries<CuteContentGenerate>(
                contentfulClient,
                "cuteContentGenerate",
                pageSize: 1,
                queryConfigurator: b => b.FieldEquals("fields.key", key)
            )
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .FirstOrDefault();
    }

    public static IReadOnlyList<CuteContentGenerate> GetAll(ContentfulClient contentfulClient)
    {
        return ContentfulEntryEnumerator
            .DeliveryEntries<CuteContentGenerate>(
                contentfulClient,
                "cuteContentGenerate",
                pageSize: 1000
            )
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .ToList();
    }
}