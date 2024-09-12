using Azure;
using Azure.AI.OpenAI;
using Contentful.Core.Extensions;
using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Lib.AiModels;
using Cute.Lib.Contentful.BulkActions.Models;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Contentful.GraphQL;
using Cute.Lib.Exceptions;
using Cute.Lib.RateLimiters;
using Cute.Lib.Scriban;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Scriban;
using Scriban.Runtime;
using System.Text;

namespace Cute.Lib.Contentful.BulkActions.Actions;

public class GenerateBulkAction(
        ContentfulConnection contentfulConnection,
        HttpClient httpClient,
        ContentfulGraphQlClient graphQlClient,
        IAzureOpenAiOptionsProvider azureOpenAiOptionsProvider,
        IReadOnlyDictionary<string, string?> appSettings)
    : BulkActionBase(contentfulConnection, httpClient)

{
    private readonly ContentfulGraphQlClient _graphQlClient = graphQlClient;

    private readonly IAzureOpenAiOptionsProvider _azureOpenAiOptionsProvider = azureOpenAiOptionsProvider;

    private readonly IReadOnlyDictionary<string, string?> _appSettings = appSettings;

    private Dictionary<string, ContentType>? _withContentTypes;

    public GenerateBulkAction WithContentTypes(IEnumerable<ContentType> contentTypes)
    {
        _withContentTypes = contentTypes.ToDictionary(ct => ct.SystemProperties.Id);
        return this;
    }

    public override IList<ActionProgressIndicator> ActionProgressIndicators() =>
        [
            new() { Intent = "Generating content..." },
    ];

    public override Task ExecuteAsync(Action<BulkActionProgressEvent>[]? progressUpdaters = null)
    {
        // Need to be refactored
        throw new NotImplementedException();
    }

    public async Task GenerateContent(string metaPromptKey,
        DisplayActions displayActions,
        Action<int, int>? progressUpdater = null,
        bool testOnly = false,
        DataFilter? dataFilter = null,
        string[]? modelNames = null)
    {
        displayActions.DisplayRuler?.Invoke();
        displayActions.DisplayBlankLine?.Invoke();
        displayActions.DisplayFormatted?.Invoke($"Reading prompt entry {metaPromptKey}...");
        displayActions.DisplayBlankLine?.Invoke();

        var metaPrompt = CuteContentGenerate.GetByKey(_contentfulConnection.PreviewClient, metaPromptKey)
            ?? throw new CliException($"No 'cuteContentGenerate' entry with key '{metaPromptKey}' found.");

        displayActions.DisplayFormatted?.Invoke($"Executing query {metaPrompt.CuteDataQueryEntry.Key}...");
        displayActions.DisplayBlankLine?.Invoke();

        var queryResult = await GetQueryData(metaPrompt)
            ?? throw new CliException($"No data found to process. Is your query valid and tested?");

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
    }

    private async Task ProcessQueryResultsForModels(CuteContentGenerate metaPrompt, JArray queryResult,
        DisplayActions displayActions,
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

    private async Task ProcessQueryResults(CuteContentGenerate metaPrompt, JArray queryResult,
        DisplayActions displayActions,
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

        var variableName = metaPrompt.CuteDataQueryEntry.VariablePrefix.Trim('.');

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

                DisplayLines(systemMessage, displayActions.DisplayDim);

                displayActions.DisplayBlankLine?.Invoke();

                displayActions.DisplayHeading?.Invoke("Prompt:");

                DisplayLines(prompt, displayActions.DisplayDim);
            }

            displayActions.DisplayBlankLine?.Invoke();

            var modelNameAsString = modelName == null ? string.Empty : $"[{modelName}] ";

            displayActions.DisplayHeading?.Invoke($"{modelNameAsString}Response:");

            var promptResult = FixFormatting(await SendPromptToModel(chatClient, chatCompletionOptions, systemMessage, prompt));

            DisplayLines(promptResult, displayActions.DisplayNormal);

            displayActions.DisplayBlankLine?.Invoke();

            if (!testOnly)
            {
                await UpdateContentfulEntry(metaPrompt, entry, promptResult, displayActions);
            }

            scriptObject.Remove(variableName);
        }
    }

    // makes sure all bullets are consistently formatted ("- ")
    // ..removes blank lines
    // ..and trims leading spaces
    private static string FixFormatting(ReadOnlySpan<char> spanInput)
    {
        var result = new StringBuilder(spanInput.Length);

        int lineStart = 0;
        while (lineStart < spanInput.Length)
        {
            // Find the end of the current line
            int lineEnd = spanInput[lineStart..].IndexOfAny('\r', '\n');
            if (lineEnd == -1)
            {
                lineEnd = spanInput.Length;
            }
            else
            {
                lineEnd += lineStart; // Adjust to the actual index in spanInput
            }

            // Extract the current line as a span
            ReadOnlySpan<char> line = spanInput[lineStart..lineEnd];

            // Move to the next line for the next iteration, handling \r\n
            if (lineEnd < spanInput.Length - 1 && spanInput[lineEnd] == '\r' && spanInput[lineEnd + 1] == '\n')
            {
                lineStart = lineEnd + 2; // Skip both \r and \n
            }
            else
            {
                lineStart = lineEnd + 1; // Skip just the \r or \n
            }

            // Trim the leading spaces
            line = line.TrimStart();

            if (line.Length == 0)
            {
                // Skip blank lines
                continue;
            }

            // Check if the line starts with '*', '-', or '+'
            if (line.Length > 0 && (line[0] == '*' || line[0] == '-' || line[0] == '+'))
            {
                // Replace with "- " and remove spaces after it
                result.Append('-');
                result.Append(' ');

                // Find the first non-space after * or - or +
                int firstNonSpaceIndex = 1;
                while (firstNonSpaceIndex < line.Length && line[firstNonSpaceIndex] == ' ')
                {
                    firstNonSpaceIndex++;
                }

                result.Append(line[firstNonSpaceIndex..]);
            }
            else
            {
                // Append the line as is if it doesn't start with the special characters
                result.Append(line);
            }

            result.AppendLine(); // Add a newline after processing each line
        }

        // Remove the last newline without allocating a new string
        if (result.Length > 0 && result[^1] == '\n')
        {
            result.Length--; // Remove '\n'
            if (result.Length > 0 && result[^1] == '\r')
            {
                result.Length--; // Remove '\r'
            }
        }

        return result.ToString();
    }

    private static void DisplayLines(ReadOnlySpan<char> input, Action<string>? displayAction, int maxLineLength = 80)
    {
        if (displayAction == null)
            return;

        while (!input.IsEmpty)
        {
            // Find the maximum slice we can take for this line
            var length = Math.Min(maxLineLength, input.Length);
            var slice = input[..length]; // Using the range operator here for slicing

            // Find the first occurrence of \r or \n
            var firstNewlineIndex = slice.IndexOfAny('\r', '\n');
            // Find the last occurrence of a space
            var lastSpaceIndex = slice.LastIndexOf(' ');

            if (lastSpaceIndex != -1 && firstNewlineIndex > lastSpaceIndex)
            {
                lastSpaceIndex = -1;
            }

            if (lastSpaceIndex != -1 && length < maxLineLength)
            {
                lastSpaceIndex = -1;
            }

            // Break at the first newline character
            if (firstNewlineIndex != -1 && (firstNewlineIndex < lastSpaceIndex || lastSpaceIndex == -1))
            {
                displayAction(slice[..firstNewlineIndex].ToString()); // Using range operator

                // Handle \r\n as a single line break
                if (firstNewlineIndex + 1 < input.Length && input[firstNewlineIndex] == '\r' && input[firstNewlineIndex + 1] == '\n')
                    input = input[(firstNewlineIndex + 2)..]; // Skip \r\n using range
                else
                    input = input[(firstNewlineIndex + 1)..]; // Skip \r or \n using range

                continue;
            }

            // Break at the last space if no newline is found earlier
            if (lastSpaceIndex != -1)
            {
                displayAction(slice[..lastSpaceIndex].ToString()); // Using range operator
                input = input[(lastSpaceIndex + 1)..]; // Skip the space using range
                continue;
            }

            // If no space or newline was found, just break at max length
            displayAction(slice.ToString());
            input = input[length..]; // Move to the next chunk using range operator
        }
    }

    private static bool EntryHasExistingContent(CuteContentGenerate metaPrompt, JObject entry)
    {
        var content = entry.SelectToken(metaPrompt.PromptOutputContentField);

        if (content.IsNull()) return false;

        if (content is JArray jArray)
        {
            return jArray.Count > 0;
        }

        if (string.IsNullOrWhiteSpace(content?.Value<string>())) return false;

        return true;
    }

    private async Task UpdateContentfulEntry(CuteContentGenerate metaPrompt, JObject entry, string promptResult, DisplayActions displayActions)
    {
        var id = entry.SelectToken("$.sys.id")?.Value<string>() ??
            throw new CliException("The query needs to return a 'sys.id' for each item.");

        var obj = await RateLimiter.SendRequestAsync(() =>
            _contentfulConnection.ManagementClient.GetEntry(id),
            null,
            null,
            (m) => displayActions?.DisplayAlert?.Invoke(m.ToString())
        );

        var fields = obj.Fields as JObject ??
            throw new CliException("Weird! The entry does not have any fields?? I'd run without looking back..");

        var contentTypeId = obj.SystemProperties.ContentType.SystemProperties.Id;

        var contentType = (_withContentTypes?[contentTypeId])
            ?? throw new CliException($"The content type '{contentTypeId}' was not resolved. Did you call '.WithContentTypes' method first?");

        var fieldId = metaPrompt.PromptOutputContentField;

        var fieldDefinition = contentType.Fields.FirstOrDefault(f => f.Id == fieldId)
            ?? throw new CliException($"The field '{fieldId}' was not found in the content type '{contentTypeId}'.");

        var oldValueRef = fields[fieldId];

        var locale = metaPrompt.GeneratorTargetDataLanguageEntry?.Iso2code ?? _contentLocales?.DefaultLocale ?? "en";

        if (oldValueRef is null)
        {
            oldValueRef = new JObject()
            {
                [locale] = null
            };
            fields[fieldId] = oldValueRef;
        }

        var oldValue = oldValueRef[locale];

        if (oldValue is JArray jArray)
        {
            if (jArray.Count == 0)
            {
                oldValue = null;
            }
        }

        if (!oldValue.IsNull()) return;

        if (!string.IsNullOrWhiteSpace(oldValue?.ToString())) return;

        JToken replaceValue = fieldDefinition.Type switch
        {
            "Symbol" or "Text" => promptResult,
            "RichText" => ToRichText(promptResult),
            "Array" => ToArray(promptResult, fieldDefinition.Items, fieldId, fieldDefinition),
            _ => throw new CliException($"Field '{fieldId}' is of type '{fieldDefinition.Type}' which can't store prompt results."),
        };

        oldValueRef[locale] = replaceValue;

        await RateLimiter.SendRequestAsync(() =>
            _contentfulConnection.ManagementClient.CreateOrUpdateEntry(obj, version: obj.SystemProperties.Version),
            null,
            null,
            (m) => displayActions?.DisplayAlert?.Invoke(m.ToString())
        );

        await RateLimiter.SendRequestAsync(() =>
            _contentfulConnection.ManagementClient.PublishEntry(id, (int)obj.SystemProperties.Version! + 1),
            null,
            null,
            (m) => displayActions?.DisplayAlert?.Invoke(m.ToString())
        );
    }

    private JArray ToArray(string promptResult, Schema items, string fieldId, Field fieldDefinition)
    {
        return items.Type switch
        {
            "Symbol" or "Text" => ToFormattedStringArray(promptResult),
            _ => throw new CliException($"Field '{fieldId}' is of type '{fieldDefinition.Type}' which can't store prompt results."),
        };
    }

    private JArray ToFormattedStringArray(string promptResult)
    {
        var elements = promptResult
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim([' ', '\n', '\r', '-']));

        return new JArray(elements);
    }

    private static JObject ToRichText(string promptResult)
    {
        return new JObject()
        {
            ["nodeType"] = "document",
            ["data"] = new JObject(),
            ["content"] = new JArray()
                {
                    new JObject()
                    {
                        ["nodeType"] = "paragraph",
                        ["content"] = new JArray()
                        {
                            new JObject()
                            {
                                ["nodeType"] = "text",
                                ["value"] = promptResult,
                                ["marks"] = new JArray()
                            }
                        }
                    }
                }
        };
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

    private ChatClient CreateChatClient(string? deploymentName = null, DisplayActions? displayActions = null)
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

    private async Task<JArray?> GetQueryData(CuteContentGenerate metaPrompt)
    {
        return await _graphQlClient.GetData(
            metaPrompt.CuteDataQueryEntry.Query,
            metaPrompt.CuteDataQueryEntry.JsonSelector,
            metaPrompt.GeneratorTargetDataLanguageEntry?.Iso2code ?? _contentLocales?.DefaultLocale ?? "en");
    }

    private static string RenderTemplate(ScriptObject scriptObject, Template template)
    {
        return template.Render(scriptObject, memberRenamer: member => member.Name.ToCamelCase());
    }
}