using Azure;
using Azure.AI.OpenAI;
using Contentful.Core.Extensions;
using Contentful.Core.Search;
using Cute.Lib.AiModels;
using Cute.Lib.CommandRunners.Filters;
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
        CommandRunnerDisplayActions displayActions,
        Action<int, int>? progressUpdater = null,
        bool testOnly = false,
        DataFilter? dataFilter = null,
        string[]? modelNames = null)
    {
        displayActions.DisplayRuler?.Invoke();
        displayActions.DisplayBlankLine?.Invoke();
        displayActions.DisplayFormatted?.Invoke($"Reading prompt entry {metaPromptKey}...");
        displayActions.DisplayBlankLine?.Invoke();

        var metaPrompt = await GetMetaPromptEntry(metaPromptKey);

        if (metaPrompt == null) return new CommandRunnerResult(RunnerResult.Error,
            $"No metaPrompt entry with key '{metaPromptKey}' found.");

        displayActions.DisplayFormatted?.Invoke($"Executing query {metaPrompt.UiDataQueryEntry.Key}...");
        displayActions.DisplayBlankLine?.Invoke();

        var queryResult = await GetQueryData(metaPrompt);

        if (queryResult == null) return new CommandRunnerResult(RunnerResult.Error,
            $"No data found to process. Is your query valid and tested?");

        if (dataFilter is not null)
        {
            displayActions.DisplayFormatted?.Invoke($"Applying filter...");
            displayActions.DisplayBlankLine?.Invoke();

            var filteredResult = new JArray();
            foreach (var obj in queryResult.Cast<JObject>())
            {
                if (dataFilter.Compare(obj))
                {
                    filteredResult.Add(obj);
                }
            }
            queryResult = filteredResult;
        }

        displayActions.DisplayFormatted?.Invoke($"Found {queryResult.Count} entries...");
        displayActions.DisplayBlankLine?.Invoke();

        displayActions.DisplayRuler?.Invoke();
        displayActions.DisplayBlankLine?.Invoke();

        progressUpdater?.Invoke(0, queryResult.Count);

        if (modelNames == null || modelNames.Length == 0)
        {
            await ProcessQueryResults(metaPrompt, queryResult, displayActions, progressUpdater, testOnly);
        }
        else
        {
            await ProcessQueryResultsForModels(metaPrompt, queryResult, displayActions, progressUpdater, testOnly, modelNames);
        }

        return new CommandRunnerResult(RunnerResult.Success);
    }

    private async Task ProcessQueryResultsForModels(MetaPrompt metaPrompt, JArray queryResult,
        CommandRunnerDisplayActions displayActions,
        Action<int, int>? progressUpdater,
        bool testOnly,
        string[] modelNames)
    {
        var first = true;

        foreach (var modelName in modelNames)
        {
            await ProcessQueryResults(metaPrompt, queryResult, displayActions, progressUpdater, testOnly, modelName, first);

            first = false;
        }
    }

    private async Task ProcessQueryResults(MetaPrompt metaPrompt, JArray queryResult,
        CommandRunnerDisplayActions displayActions,
        Action<int, int>? progressUpdater,
        bool testOnly,
        string? modelName = null,
        bool displaySystemMessageAndPrompt = true)
    {
        var scriptObject = CreateScriptObject();

        var chatClient = CreateChatClient(modelName, displayActions);

        var chatCompletionOptions = new ChatCompletionOptions()
        {
            MaxTokens = metaPrompt.MaxTokenLimit,
            Temperature = (float)metaPrompt.Temperature,
            FrequencyPenalty = (float)metaPrompt.FrequencyPenalty,
            PresencePenalty = (float)metaPrompt.PresencePenalty,
            TopP = (float)metaPrompt.TopP
        };

        if (modelName is null)
        {
            modelName = metaPrompt.DeploymentModel;
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = null;
            }
        }

        var promptTemplate = Template.Parse(metaPrompt.Prompt);

        var systemTemplate = Template.Parse(metaPrompt.SystemMessage);

        var variableName = metaPrompt.UiDataQueryEntry.VariablePrefix.Trim('.');

        var recordNum = 1;
        var recordTotal = queryResult.Count;

        foreach (var entry in queryResult.Cast<JObject>())
        {
            progressUpdater?.Invoke(recordNum++, recordTotal);

            if (EntryHasExistingContent(metaPrompt, entry) && !testOnly)
            {
                continue;
            }

            var entryKey = entry["key"]?.Value<string>()
                ?? "(unknown entry key)";

            var entryTitle = entry["title"]?.Value<string>()
                ?? entry["name"]?.Value<string>()
                ?? "(unknown entry title)";

            displayActions.DisplayAlert?.Invoke($"[{entryKey}] : [{entryTitle}]");
            displayActions.DisplayBlankLine?.Invoke();

            scriptObject.SetValue(variableName, entry, true);

            var systemMessage = RenderTemplate(scriptObject, systemTemplate);

            var prompt = RenderTemplate(scriptObject, promptTemplate);

            displayActions.DisplayRuler?.Invoke();

            if (displaySystemMessageAndPrompt)
            {
                displayActions.DisplayHeading?.Invoke("System Message:");

                foreach (var s in SplitAndFormatString(systemMessage))
                {
                    displayActions.DisplayDim?.Invoke(s);
                }

                displayActions.DisplayBlankLine?.Invoke();

                displayActions.DisplayHeading?.Invoke("Prompt:");

                foreach (var s in SplitAndFormatString(prompt))
                {
                    displayActions.DisplayDim?.Invoke(s);
                }
            }

            displayActions.DisplayBlankLine?.Invoke();

            var modelNameAsString = modelName == null ? string.Empty : $"[{modelName}] ";

            displayActions.DisplayHeading?.Invoke($"{modelNameAsString}Response:");

            var promptResult = FixFormatting(await SendPromptToModel(chatClient, chatCompletionOptions, systemMessage, prompt));

            displayActions.DisplayNormal?.Invoke(promptResult);

            displayActions.DisplayBlankLine?.Invoke();

            if (!testOnly)
            {
                await UpdateContentfulEntry(metaPrompt, entry, promptResult, displayActions);
            }

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
                [metaPrompt.GeneratorTargetDataLanguageEntry.Iso2code] = null
            };
            fields[metaPrompt.PromptOutputContentField] = oldValueRef;
        }

        var oldValue = oldValueRef![metaPrompt.GeneratorTargetDataLanguageEntry.Iso2code];

        if (!oldValue.IsNull()) return;

        if (!string.IsNullOrWhiteSpace(oldValue?.ToString())) return;

        oldValueRef[metaPrompt.GeneratorTargetDataLanguageEntry.Iso2code] = promptResult;

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
                displayActions.DisplayAlert?.Invoke(ex.Message);
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
                displayActions.DisplayAlert?.Invoke(ex.Message);
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

    private ChatClient CreateChatClient(string? deploymentName = null, CommandRunnerDisplayActions? displayActions = null)
    {
        var options = _azureOpenAiOptionsProvider.GetAzureOpenAIClientOptions();

        if (deploymentName == null)
        {
            deploymentName = options.DeploymentName;
            displayActions?.DisplayFormatted?.Invoke($"Model deployment: {deploymentName}");
            displayActions?.DisplayBlankLine?.Invoke();
        }

        AzureOpenAIClient client = new(
            new Uri(options.Endpoint),
            new AzureKeyCredential(options.ApiKey)
        );

        return client.GetChatClient(deploymentName);
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
            metaPrompt.GeneratorTargetDataLanguageEntry.Iso2code);
    }

    private static string RenderTemplate(ScriptObject scriptObject, Template template)
    {
        return template.Render(scriptObject, memberRenamer: member => member.Name.ToCamelCase());
    }
}