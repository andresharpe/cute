using Azure.AI.OpenAI;
using Contentful.Core.Models;
using Cute.Lib.AiModels;
using Cute.Lib.Cache;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.InputAdapters.Http.Models;
using Cute.Lib.InputAdapters.MemoryAdapters;
using Cute.Lib.Serializers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Scriban;
using Scriban.Syntax;
using System.ClientModel;
using System.IO.Hashing;
using System.Text;

namespace Cute.Lib.InputAdapters.Http;

public class OpenAiInputAdapter(
    OpenAiDataAdapterConfig adapter,
    ContentfulConnection contentfulConnection,
    ContentLocales contentLocales,
    IReadOnlyDictionary<string, string?> envSettings,
    IEnumerable<ContentType> contentTypes,
    IAzureOpenAiOptionsProvider azureOpenAiOptionsProvider)
    : WebInputAdapter("OpenAi", adapter, contentfulConnection, envSettings)
{
    private readonly OpenAiDataAdapterConfig _adapter = adapter;

    private readonly ContentLocales _contentLocales = contentLocales;

    private readonly IAzureOpenAiOptionsProvider _azureOpenAiOptionsProvider = azureOpenAiOptionsProvider;

    public override async Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        if (_currentRecordIndex == -1)
        {
            await GetRecordCountAsync();
        }

        if (_currentRecordIndex >= _results.Count)
        {
            return null;
        }

        var result = _serializer.CreateNewFlatEntry(_results[_currentRecordIndex]);

        _currentRecordIndex++;

        return result;
    }

    public override async Task<int> GetRecordCountAsync()
    {
        if (_results is not null && _results.Count > 0) return _results.Count;

        _contentType = contentTypes.FirstOrDefault(ct => ct.SystemProperties.Id == _adapter.ContentType)
            ?? throw new CliException($"Content type '{_adapter.ContentType}' does not exist.");

        _serializer = new EntrySerializer(_contentType, _contentLocales);
        FormUrlEncodedContent? formContent = null;

        _results = await MakeHttpCall(formContent);

        _currentRecordIndex = 0;

        return _results.Count;
    }

    private ChatClient CreateChatClient(string? deploymentName = null, DisplayActions? displayActions = null)
    {
        var options = _azureOpenAiOptionsProvider.GetAzureOpenAIClientOptions();

        var chatCompletionOptions = new ChatCompletionOptions()
        {
            //MaxOutputTokenCount = cuteContentGenerateEntry.MaxTokenLimit,
            //Temperature = (float)cuteContentGenerateEntry.Temperature,
            //FrequencyPenalty = (float)cuteContentGenerateEntry.FrequencyPenalty,
            //PresencePenalty = (float)cuteContentGenerateEntry.PresencePenalty,
            //TopP = (float)cuteContentGenerateEntry.TopP
        };

        if (deploymentName == null)
        {
            deploymentName = options.DeploymentName;
            displayActions?.DisplayFormatted?.Invoke($"Model deployment: {deploymentName}");
            displayActions?.DisplayBlankLine?.Invoke();
        }

        AzureOpenAIClient client = new(
            new Uri(options.Endpoint),
            new ApiKeyCredential(options.ApiKey)
        );

        return client.GetChatClient(deploymentName);
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

    private async Task<List<Dictionary<string, string>>> MakeHttpCall(FormUrlEncodedContent? formContent)
    {

        var returnValue = new List<Dictionary<string, string>>();

        var client = CreateChatClient();

        var response = await SendPromptToModel(client, new ChatCompletionOptions(), _adapter.SystemMessage, _adapter.Prompt);

        JArray rootArray = JArray.Parse(response);

        var batchValue = MapResultValues(rootArray);

        return [.. returnValue.OrderBy(e => e[_adapter.ContentKeyField])];
    }

    private Dictionary<string, string> CompileValuesWithEnvironment(Dictionary<string, string> headers)
    {
        var templates = headers.ToDictionary(m => m.Key, m => Template.Parse(m.Value));

        var errors = new List<string>();

        foreach (var (header, template) in templates)
        {
            if (template.HasErrors)
            {
                errors.Add($"Error(s) in mapping for header '{header}'.{template.Messages.Select(m => $"\n...{m.Message}")} ");
            }
        }

        if (errors.Count != 0) throw new CliException(string.Join('\n', errors));

        try
        {
            var newRecord = templates.ToDictionary(t => t.Key, t => t.Value.Render(_scriptObject));

            return newRecord;
        }
        catch (ScriptRuntimeException e)
        {
            throw new CliException(e.Message, e);
        }
    }

    private static ContentEntryEnumerators? GetEntryEnumerators(
        List<ContentEntryDefinition>? entryDefinitions,
        ContentfulConnection contentfulConnection,
        IEnumerable<ContentType> contentTypes)
    {
        if (entryDefinitions is null) return null;

        if (entryDefinitions.Count == 0) return null;

        var contentEntryEnumerators = new ContentEntryEnumerators();
        foreach (var entryDefinition in entryDefinitions)
        {
            var contentType = contentTypes.FirstOrDefault(ct => ct.SystemProperties.Id == entryDefinition.ContentType)
                ?? throw new CliException($"Content type '{entryDefinition.ContentType}' does not exist.");

            var enumerator = contentfulConnection.GetManagementEntries<Entry<JObject>>(
                    new EntryQuery.Builder()
                        .WithContentType(entryDefinition.ContentType)
                        .WithOrderByField(contentType.DisplayField)
                        .WithQueryString(entryDefinition.QueryParameters)
                        .Build()
                );

            contentEntryEnumerators.Add(enumerator);
        }

        return contentEntryEnumerators;
    }
}