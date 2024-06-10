using Cut.Services;
using Spectre.Console.Cli;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Spectre.Console;
using Cut.Lib.OutputAdapters;
using System.ComponentModel;
using Contentful.Core.Models;
using Contentful.Core.Search;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Linq;
using Cut.Lib.Exceptions;
using Cut.Lib.Contentful;
using System.Text.RegularExpressions;
using System.Text;

namespace Cut.Commands;

public class GenerateCommand : LoggedInCommand<GenerateCommand.Settings>
{
    private static readonly Regex _regex = new("{{(?'ContentId'[a-zA-Z_0-9]+)\\.(?'FieldId'[a-zA-Z_0-9]+)}}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public GenerateCommand(IConsoleWriter console, IPersistedTokenCache tokenCache)
        : base(console, tokenCache)
    {
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-t|--prompt-title")]
        [Description("The title of the Contentful prompt entry to generate content from.")]
        public string PromptTitle { get; set; } = default!;

        [CommandOption("-l|--limit")]
        [Description("The total number of entries to generate content for before stopping. Default is five.")]
        public int Limit { get; set; } = 5;

        [CommandOption("-s|--skip")]
        [Description("The total number of entries to skip before starting. Default is zero.")]
        public int Skip { get; set; } = 0;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        if (result != 0 || _contentfulClient == null || _appSettings == null) return result;

        var defaultLocale = (await _contentfulClient.GetLocalesCollection())
            .First(l => l.Default)
            .Code;

        var promptQuery = new QueryBuilder<Dictionary<string, object?>>()
             .ContentTypeIs("prompts")
             .Limit(1)
             .FieldEquals("fields.title", settings.PromptTitle)
             .Build();

        var promptEntries = await _contentfulClient.GetEntriesCollection<Entry<JObject>>(promptQuery);

        if (!promptEntries.Any())
        {
            throw new CliException($"No prompt with title '{settings.PromptTitle}' found.");
        }

        var promptEntry = promptEntries.First();

        var promptContentTypeId = promptEntry.Fields["contentTypeId"]?[defaultLocale]?.Value<string>()
            ?? throw new CliException($"Prompt '{settings.PromptTitle}' does not contain a valid contentTypeId");

        var promptContentFieldId = promptEntry.Fields["contentFieldId"]?[defaultLocale]?.Value<string>()
            ?? throw new CliException($"Prompt '{settings.PromptTitle}' does not contain a valid contentFieldId");

        var promptSystemMessage = promptEntry.Fields["systemMessage"]?[defaultLocale]?.Value<string>()
            ?? throw new CliException($"Prompt '{settings.PromptTitle}' does not contain a valid systemMessage");

        var promptMainPrompt = promptEntry.Fields["mainPrompt"]?[defaultLocale]?.Value<string>()
            ?? throw new CliException($"Prompt '{settings.PromptTitle}' does not contain a valid mainPrompt");

        var contentType = await _contentfulClient.GetContentType(promptContentTypeId);

        if (contentType.Fields.FirstOrDefault(f => f.Id.Equals(promptContentFieldId)) == null)
        {
            throw new CliException($"{promptContentFieldId} does not exist in content type {contentType.SystemProperties.Id}");
        }

        AzureOpenAIClient client = new(
          new Uri(_appSettings.OpenAiEndpoint),
          new AzureKeyCredential(_appSettings.OpenAiApiKey));

        var chatClient = client.GetChatClient(_appSettings.OpenAiDeploymentName);

        var chatCompletionOptions = new ChatCompletionOptions()
        {
            Temperature = (float)0.7,
            MaxTokens = 800,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
        };

        var match = _regex.Match(promptMainPrompt);

        var entries = EntryEnumerator.Entries(_contentfulClient, contentType.SystemProperties.Id, contentType.DisplayField);

        foreach (var (entry, _) in entries.Skip(settings.Skip).Take(settings.Limit))
        {
            if (!string.IsNullOrEmpty(entry.Fields[promptContentFieldId]?[defaultLocale]?.Value<string>()))
            {
                continue;
            }

            _console.WriteBlankLine();
            _console.WriteBlankLine();

            List<ChatMessage> messages = [
                new SystemChatMessage(promptSystemMessage),
                new UserChatMessage(ReplaceFields(promptMainPrompt, entry, defaultLocale, match)),
            ];

            var sb = new StringBuilder();

            await foreach (var part in chatClient.CompleteChatStreamingAsync(messages, chatCompletionOptions))
            {
                if (part == null || part.ToString() == null) continue;

                foreach (var token in part.ContentUpdate)
                {
                    sb.Append(token.Text);
                    AnsiConsole.Write(token.Text);
                    await Task.Delay(20);
                }
            }

            if (entry.Fields[promptContentFieldId] == null)
            {
                entry.Fields[promptContentFieldId] = new JObject(new JProperty(defaultLocale, sb.ToString()));
            }
            else if (entry.Fields[promptContentFieldId] is JObject existingValues)
            {
                if (existingValues[defaultLocale] == null)
                {
                    existingValues.Add(new JProperty(defaultLocale, sb.ToString()));
                }
                else
                {
                    existingValues[defaultLocale] = sb.ToString();
                }
            }

            var updatedEntry = await _contentfulClient.CreateOrUpdateEntry<JObject>(entry.Fields, id: entry.SystemProperties.Id, version: entry.SystemProperties.Version);
            _ = await _contentfulClient.PublishEntry(entry.SystemProperties.Id, entry.SystemProperties.Version!.Value + 1);
        }

        return 0;
    }

    private static string ReplaceFields(string prompt, Entry<JObject> entry, string defaultLocale, Match match)
    {
        var newPrompt = prompt;

        foreach (Capture capture in match.Groups["FieldId"].Captures.Cast<Capture>())
        {
            var value = entry.Fields[capture.Value]?[defaultLocale]?.Value<string>();

            newPrompt = _regex.Replace(newPrompt, value!);
        }

        return newPrompt;
    }
}