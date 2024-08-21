using Azure;
using Azure.AI.OpenAI;
using Contentful.Core.Extensions;
using Contentful.Core.Search;
using Cute.Lib.AiModels;
using Cute.Lib.CommandRunners.Models;
using Cute.Lib.Contentful;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.GraphQL;
using Cute.Lib.Scriban;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Scriban;
using Scriban.Runtime;
using System.Text;

namespace Cute.Lib.CommandRunners;

public class GenerateCommandRunner
{
    private readonly ContentfulConnection _contentfulConnection;

    private readonly ContentfulGraphQlClient _graphQlClient;

    private readonly IAzureOpenAiOptionsProvider _azureOpenAiOptionsProvider;

    private readonly IReadOnlyDictionary<string, string?> _appSettings;

    public GenerateCommandRunner(ContentfulConnection contentfulConnection,
        ContentfulGraphQlClient graphQlClient,
        IAzureOpenAiOptionsProvider azureOpenAiOptionsProvider,
        IReadOnlyDictionary<string, string?> appSettings)
    {
        _contentfulConnection = contentfulConnection;
        _graphQlClient = graphQlClient;
        _azureOpenAiOptionsProvider = azureOpenAiOptionsProvider;
        _appSettings = appSettings;
    }

    public async Task<CommandRunnerResult> GenerateContent(string metaPromptKey,
        Action<int, int> progressUpdater,
        CommandRunnerDisplayActions displayActions)
    {
        displayActions.DisplayRuler?.Invoke();
        displayActions.DisplayBlankLine?.Invoke();
        displayActions.DisplayFormattedAction?.Invoke($"Reading prompt entry {metaPromptKey}...");
        displayActions.DisplayBlankLine?.Invoke();

        var metaPrompt = await GetMetaPromptEntry(metaPromptKey);

        if (metaPrompt == null) return new CommandRunnerResult(RunnerResult.Error,
            $"No metaPrompt entry with key '{metaPromptKey}' found.");

        displayActions.DisplayFormattedAction?.Invoke($"Executing query {metaPrompt.UiDataQueryEntry.Key}...");
        displayActions.DisplayBlankLine?.Invoke();

        var queryResult = await GetQueryData(metaPrompt);

        if (queryResult == null) return new CommandRunnerResult(RunnerResult.Error,
            $"No data found to process. Is your query valid and tested?");

        displayActions.DisplayRuler?.Invoke();
        displayActions.DisplayBlankLine?.Invoke();

        progressUpdater.Invoke(0, queryResult.Count);

        await ProcessQueryResults(metaPrompt, queryResult, progressUpdater, displayActions);

        return new CommandRunnerResult(RunnerResult.Success);
    }

    private async Task ProcessQueryResults(MetaPrompt metaPrompt, JArray queryResult,
        Action<int, int> progressUpdater, CommandRunnerDisplayActions displayActions)
    {
        var scriptObject = CreateScriptObject();

        var chatClient = CreateChatClient();

        var chatCompletionOptions = new ChatCompletionOptions()
        {
            Temperature = (float)metaPrompt.Temperature,
            FrequencyPenalty = (float)metaPrompt.FrequencyPenalty,
            PresencePenalty = 0,
        };

        var promptTemplate = Template.Parse(metaPrompt.Prompt);

        var systemTemplate = Template.Parse(metaPrompt.SystemMessage);

        var variableName = metaPrompt.UiDataQueryEntry.VariablePrefix.Trim('.');

        var recordNum = 1;
        var recordTotal = queryResult.Count;

        foreach (var entry in queryResult.Cast<JObject>())
        {
            progressUpdater.Invoke(recordNum++, recordTotal);

            if (EntryHasExistingContent(metaPrompt, entry))
            {
                continue;
            }

            var title = entry["title"]?.Value<string>()
                ?? entry["name"]?.Value<string>()
                ?? entry["key"]?.Value<string>()
                ?? "(unknown entry)";

            displayActions.DisplayAlertAction?.Invoke($"[{title}]");
            displayActions.DisplayBlankLine?.Invoke();

            scriptObject.SetValue(variableName, entry, true);

            var systemMessage = RenderTemplate(scriptObject, systemTemplate);

            var prompt = RenderTemplate(scriptObject, promptTemplate);

            displayActions.DisplayRuler?.Invoke();

            foreach (var s in SplitAndFormatString(systemMessage))
            {
                displayActions.DisplayDimAction?.Invoke(s);
            }

            displayActions.DisplayBlankLine?.Invoke();

            foreach (var s in SplitAndFormatString(prompt))
            {
                displayActions.DisplayDimAction?.Invoke(s);
            }

            displayActions.DisplayBlankLine?.Invoke();

            var promptResult = FixFormatting(await SendPromptToModel(chatClient, chatCompletionOptions, systemMessage, prompt));

            displayActions.DisplayAction?.Invoke(promptResult);

            displayActions.DisplayBlankLine?.Invoke();

            await UpdateContentfulEntry(metaPrompt, entry, promptResult, displayActions);

            scriptObject.Remove(variableName);
        }
    }

    private static string FixFormatting(string text)
    {
        return string.Join('\n', SplitAndFormatString(text));
    }

    private static IEnumerable<string> SplitAndFormatString(string text)
    {
        return text.Split('\n')
            .Select(l => l.TrimStart().StartsWith('-')
                ? "- " + l.Trim().TrimStart('-').TrimStart()
                : l.Trim()
            )
            .Where(l => !string.IsNullOrWhiteSpace(l));
    }

    private static bool EntryHasExistingContent(MetaPrompt metaPrompt, JObject entry)
    {
        var content = entry.SelectToken(metaPrompt.PromptOutputContentField);

        if (content.IsNull()) return false;

        if (string.IsNullOrWhiteSpace(content?.Value<string>())) return false;

        return true;
    }

    private async Task UpdateContentfulEntry(MetaPrompt metaPrompt, JObject entry, string promptResult, CommandRunnerDisplayActions displayActions)
    {
        var id = entry.SelectToken("$.sys.id")?.Value<string>() ??
            throw new CliException("The query needs to return a 'sys.id' for each item.");

        var obj = await _contentfulConnection.ManagementClient.GetEntry(id);

        var fields = obj.Fields as JObject ??
            throw new CliException("Weird! The entry does not have any fields?? I'd run without looking back..");

        var oldValueRef = fields[metaPrompt.PromptOutputContentField];

        if (oldValueRef is null)
        {
            oldValueRef = new JObject()
            {
                [metaPrompt.GeneratorTargetLanguage.Iso2code] = null
            };
            fields[metaPrompt.PromptOutputContentField] = oldValueRef;
        }

        var oldValue = oldValueRef![metaPrompt.GeneratorTargetLanguage.Iso2code];

        if (!oldValue.IsNull()) return;

        if (!string.IsNullOrWhiteSpace(oldValue?.ToString())) return;

        oldValueRef[metaPrompt.GeneratorTargetLanguage.Iso2code] = promptResult;

        var isCreated = false;

        while (!isCreated)
        {
            try
            {
                await _contentfulConnection.ManagementClient.CreateOrUpdateEntry(obj, version: obj.SystemProperties.Version);
                isCreated = true;
            }
            catch (Exception ex)
            {
                displayActions.DisplayAlertAction?.Invoke(ex.Message);
                await Task.Delay(100);
            }
        }

        var isPublished = false;

        while (!isPublished)
        {
            try
            {
                await _contentfulConnection.ManagementClient.PublishEntry(id, (int)obj.SystemProperties.Version! + 1);
                isPublished = true;
            }
            catch (Exception ex)
            {
                displayActions.DisplayAlertAction?.Invoke(ex.Message);
                await Task.Delay(100);
            }
        }
    }

    private static async Task<string> SendPromptToModel(ChatClient chatClient, ChatCompletionOptions chatCompletionOptions, string systemMessage, string prompt)
    {
        List<ChatMessage> messages = [
                new SystemChatMessage(systemMessage),
                new UserChatMessage(prompt),
        ];

        var sb = new StringBuilder();

        var delay = 0;

        await foreach (var part in chatClient.CompleteChatStreamingAsync(messages, chatCompletionOptions))
        {
            if (part == null || part.ToString() == null) continue;

            foreach (var token in part.ContentUpdate)
            {
                sb.Append(token.Text);
                if (delay > 0)
                {
                    Console.Write(token.Text);
                    await Task.Delay(delay);
                }
            }
        }

        return sb.ToString();
    }

    private ScriptObject CreateScriptObject()
    {
        ScriptObject? scriptObject = [];

        CuteFunctions.ContentfulManagementClient = _contentfulConnection.ManagementClient;

        CuteFunctions.ContentfulClient = _contentfulConnection.DeliveryClient;

        scriptObject.SetValue("cute", new CuteFunctions(), true);

        scriptObject.SetValue("config", _appSettings, true);

        return scriptObject;
    }

    private ChatClient CreateChatClient()
    {
        var options = _azureOpenAiOptionsProvider.GetAzureOpenAIClientOptions();

        AzureOpenAIClient client = new(
            new Uri(options.Endpoint),
            new AzureKeyCredential(options.ApiKey)
        );

        return client.GetChatClient(options.DeploymentName);
    }

    private async Task<MetaPrompt?> GetMetaPromptEntry(string metaPromptKey)
    {
        await foreach (var (entry, _) in ContentfulEntryEnumerator.DeliveryEntries<MetaPrompt>(
                _contentfulConnection!.DeliveryClient,
                "metaPrompt",
                pageSize: 1,
                queryConfigurator: b => b.FieldEquals("fields.key", metaPromptKey)

            ))
        {
            return entry;
        }
        return null;
    }

    private async Task<JArray?> GetQueryData(MetaPrompt metaPrompt)
    {
        return await _graphQlClient.GetData(
            metaPrompt.UiDataQueryEntry.Query,
            metaPrompt.UiDataQueryEntry.JsonSelector,
            metaPrompt.GeneratorTargetLanguage.Iso2code);
    }

    private static string RenderTemplate(ScriptObject scriptObject, Template template)
    {
        return template.Render(scriptObject, memberRenamer: member => member.Name.ToCamelCase());
    }
}