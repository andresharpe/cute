using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

public sealed class EvaluateCommand : LoggedInCommand<EvaluateCommand.Settings>
{
    private readonly HttpClient _httpClient;

    public EvaluateCommand(IConsoleWriter console, ILogger<EvaluateCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings, HttpClient httpClient)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _httpClient = httpClient;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-g|--generation")]
        [Description("The generation metric to evaluate. Can be 'answer', 'faithfulness' or 'all'.")]
        public string? GenerationMetric { get; set; } = default!;

        [CommandOption("-t|--translation")]
        [Description("The translation metric to evaluate. Can be 'gleu', 'meteor', 'lepor', or 'all'.")]
        public string? TranslationMetric { get; set; } = default!;

        [CommandOption("-s|--seo")]
        [Description("The seo metric to evaluate.")]
        public string? SeoMetric { get; set; } = default!;

        [CommandOption("-i|--prompt-id")]
        [Description("The id of the Contentful prompt entry to generate prompts from.")]
        public string PromptId { get; set; } = default!;

        [CommandOption("-p|--prompt-field")]
        [Description("The field containing the prompt template for the LLM.")]
        public string PromptField { get; set; } = default!;

        [CommandOption("-c|--generated-content")]
        [Description("The field containing the LLM's generated content.")]
        public string GeneratedContentField { get; set; } = default!;

        [CommandOption("-r|--reference-content")]
        [Description("The field containing the reference content.")]
        public string ReferenceContentField { get; set; } = default!;

        [CommandOption("-f|--facts")]
        [Description("The field containing the facts for the generation evaluation.")]
        public string FactsField { get; set; } = default!;

        [CommandOption("-k|--keyword")]
        [Description("The keyword used to evaluate SEO.")]
        public string KeywordField { get; set; } = default!;

        [CommandOption("-w|--related-keywords")]
        [Description("The list of related keywords to evaluate SEO.")]
        public string RelatedKeywordsField { get; set; } = default!;

        [CommandOption("-u|--seo-input-method")]
        [Description("The input method to evaluate SEO. Can be 'url' or 'content'. Default is 'url'.")]
        public string SeoInputField { get; set; } = "url";

        [CommandOption("-h|--threshold")]
        [Description("The threshold to either pass or fail the metric's evaluation. Default is 0.7.")]
        public float Threshold { get; set; } = 0.7f;

        [CommandOption("-m|--llm-model")]
        [Description("The LLM model to use for the evaluation. Default is 'gpt-4o'.")]
        public string LlmModel { get; set; } = "gpt-4o";
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await base.ExecuteAsync(context, settings);

        var commandOptions = GetOptions(settings);

        var envSettings = _appSettings.GetSettings()
            .Where(kv => kv.Key.StartsWith("Cute__OpenAi"))
            .ToDictionary();

        string apiCall = string.Empty;

        if (settings.GenerationMetric is not null && settings.TranslationMetric is null && settings.SeoMetric is null)
        {
            apiCall = $"generator/{settings.GenerationMetric.ToLower()}";
        }
        else if (settings.TranslationMetric is not null && settings.GenerationMetric is null && settings.SeoMetric is null)
        {
            apiCall = $"translator/{settings.TranslationMetric.ToLower()}";
        }
        else if (settings.SeoMetric is not null && settings.GenerationMetric is null && settings.TranslationMetric is null)
        {
            apiCall = $"seo";
        }
        else
        {
            throw new CliException("No valid metric provided for evaluation");
        }

        var endPoint = $"http://localhost:5555/api/{apiCall}";

        _console.WriteNormalWithHighlights($"Calling eval API on '{endPoint}'...", Globals.StyleHeading);

        var result = await _httpClient.PostAsJsonAsync(endPoint,
            new { options = commandOptions, env = envSettings });

        _console.WriteRuler();

        var content = await result.Content.ReadAsStringAsync();

        _console.WriteSubHeading(JValue.Parse(content).ToString(Formatting.Indented));

        _console.WriteRuler();

        return 0;
    }
}