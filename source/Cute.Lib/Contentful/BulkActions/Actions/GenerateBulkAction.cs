using Azure;
using Azure.AI.OpenAI;
using Contentful.Core.Extensions;
using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Lib.AiModels;
using Cute.Lib.AzureOpenAi.Batch;
using Cute.Lib.Contentful.BulkActions.Models;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Contentful.GraphQL;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.Scriban;
using Cute.Lib.Serializers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Scriban;
using Scriban.Runtime;
using System.Text;

namespace Cute.Lib.Contentful.BulkActions.Actions;

public enum GenerateOperation
{
    GenerateSingle,
    GenerateParallel,
    GenerateBatch,
    ListBatches,
}

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

    private GenerateOperation _operation = GenerateOperation.GenerateBatch;

    public GenerateBulkAction WithContentTypes(IEnumerable<ContentType> contentTypes)
    {
        _withContentTypes = contentTypes.ToDictionary(ct => ct.SystemProperties.Id);
        return this;
    }

    public GenerateBulkAction WithGenerateOperation(GenerateOperation operation)
    {
        _operation = operation;
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
        var options = _azureOpenAiOptionsProvider.GetAzureOpenAIClientOptions();

        _httpClient.BaseAddress = new Uri(options.Endpoint);

        _httpClient.DefaultRequestHeaders.Add("api-key", options.ApiKey);

        displayActions.DisplayRuler?.Invoke();
        displayActions.DisplayBlankLine?.Invoke();
        displayActions.DisplayFormatted?.Invoke($"Reading prompt entry {metaPromptKey}...");
        displayActions.DisplayBlankLine?.Invoke();

        var cuteContentGenerateEntry = CuteContentGenerate.GetByKey(_contentfulConnection, metaPromptKey)
            ?? throw new CliException($"No 'cuteContentGenerate' entry with key '{metaPromptKey}' found.");

        if (_operation == GenerateOperation.ListBatches)
        {
            await ListBatches(cuteContentGenerateEntry, displayActions);
            return;
        }

        displayActions.DisplayFormatted?.Invoke($"Executing query {cuteContentGenerateEntry.CuteDataQueryEntry.Key}...");
        displayActions.DisplayBlankLine?.Invoke();

        var queryResult = await GetQueryData(cuteContentGenerateEntry)
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

        if (!testOnly)
        {
            displayActions.DisplayFormatted?.Invoke($"Skipping non-empty {cuteContentGenerateEntry.PromptOutputContentField} entries...");
            displayActions.DisplayBlankLine?.Invoke();

            var filteredResult = new JArray();
            foreach (var obj in queryResult.Cast<JObject>())
            {
                if (EntryHasExistingContent(cuteContentGenerateEntry, obj))
                {
                    continue;
                }
                filteredResult.Add(obj);
            }
            queryResult = filteredResult;
        }

        displayActions.DisplayFormatted?.Invoke($"Found {queryResult.Count} entries...");
        displayActions.DisplayBlankLine?.Invoke();

        displayActions.DisplayRuler?.Invoke();
        displayActions.DisplayBlankLine?.Invoke();

        progressUpdater?.Invoke(0, Math.Max(1, queryResult.Count));

        if (modelNames == null || modelNames.Length == 0)
        {
            switch (_operation)
            {
                case GenerateOperation.GenerateSingle:
                    await ProcessQueryResults(cuteContentGenerateEntry, queryResult, displayActions, progressUpdater, testOnly);
                    break;

                case GenerateOperation.GenerateParallel:
                    ProcessQueryResultsInParallel(cuteContentGenerateEntry, queryResult, displayActions, progressUpdater, testOnly);
                    break;

                case GenerateOperation.GenerateBatch:
                    await ProcessQueryResultsInBatch(cuteContentGenerateEntry, queryResult, displayActions, progressUpdater, testOnly);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
        else
        {
            await ProcessQueryResultsForModels(cuteContentGenerateEntry, queryResult, displayActions, progressUpdater, testOnly, modelNames);
        }
    }

    private async Task<CuteContentGenerateBatch?> GetOpenBatchEntry(CuteContentGenerate cuteContentGenerateEntry, JArray queryResult, DisplayActions displayActions, Action<int, int>? progressUpdater, bool testOnly)
    {
        var batchEntry = CuteContentGenerateBatch
            .GetAll(_contentfulConnection)
            .Where(cb => cb.CompletedAt is null && cb.CancelledAt is null && cb.ExpiredAt is null && cb.FailedAt is null)
            .Where(cb => cb.CuteContentGenerateEntry.Sys.Id == cuteContentGenerateEntry.Sys.Id)
            .SingleOrDefault();

        if (batchEntry == null) return null;

        if (cuteContentGenerateEntry is null) return null;

        displayActions.DisplayBlankLine?.Invoke();

        var azureOpenAiBatchProcessor = new AzureOpenAiBatchProcessor(_httpClient);

        displayActions.DisplayFormatted?.Invoke($"Getting batch status(es) from Azure Open AI at '{_httpClient.BaseAddress}'...");

        var status = await azureOpenAiBatchProcessor.BatchJobStatus(batchEntry.Key)
            ?? throw new CliException("List batch status(es) from Azure Open AI failed.");

        if (status.CompletedAt is not null)
        {
            batchEntry.CompletedAt = DateTimeExtensions.FromUnix(status.CompletedAt.Value);
            batchEntry.Status = status.Status;
            await SaveCuteBatchEntry(batchEntry, displayActions);

            await ProcessBatchResults(
                azureOpenAiBatchProcessor.BatchJobResult(status.OutputFileId),
                cuteContentGenerateEntry,
                batchEntry,
                queryResult, displayActions,
                progressUpdater,
                testOnly);

            batchEntry.Status = "completed-and-applied";
            batchEntry.AppliedAt = DateTime.UtcNow.StripMilliseconds();
            batchEntry.Sys.Version++;
            await SaveCuteBatchEntry(batchEntry, displayActions);
        }
        else if (status.ExpiredAt is not null)
        {
            batchEntry.ExpiredAt = DateTimeExtensions.FromUnix(status.ExpiredAt.Value);
            batchEntry.Status = status.Status;
            await SaveCuteBatchEntry(batchEntry, displayActions);
        }
        else if (status.FailedAt is not null)
        {
            batchEntry.FailedAt = DateTimeExtensions.FromUnix(status.FailedAt.Value);
            batchEntry.Status = status.Status;
            await SaveCuteBatchEntry(batchEntry, displayActions);
        }
        else if (status.CancelledAt is not null)
        {
            batchEntry.CancelledAt = DateTimeOffset.FromUnixTimeSeconds(status.CancelledAt.Value).UtcDateTime;
            batchEntry.Status = status.Status;
            await SaveCuteBatchEntry(batchEntry, displayActions);
        }

        return batchEntry;
    }

    private async Task ProcessBatchResults(
        IAsyncEnumerable<BatchJobResultResponse> jobResults,
        CuteContentGenerate cuteContentGenerateEntry,
        CuteContentGenerateBatch batchEntry,
        JArray queryResult,
        DisplayActions displayActions,
        Action<int, int>? progressUpdater,
        bool testOnly)
    {
        var entriesDict = queryResult.Cast<JObject>()
                .ToDictionary(e => e.SelectToken("sys.id")!.Value<string>()!);

        var entryCount = 1;

        batchEntry.CompletionTokens = 0;
        batchEntry.PromptTokens = 0;
        batchEntry.TotalTokens = 0;

        displayActions.DisplayBlankLine?.Invoke();
        displayActions.DisplayFormatted?.Invoke($"Downloading and applying results...");

        await foreach (var jobResult in jobResults)
        {
            progressUpdater?.Invoke(entryCount++, queryResult.Count);
            batchEntry.CompletionTokens += jobResult.Response.Body.Usage.CompletionTokens;
            batchEntry.PromptTokens += jobResult.Response.Body.Usage.PromptTokens;
            batchEntry.TotalTokens += jobResult.Response.Body.Usage.TotalTokens;

            var objectId = jobResult.CustomId.Split('|')[1];

            if (!entriesDict.TryGetValue(objectId, out var entry)) continue;

            var promptResult = jobResult.Response.Body.Choices[0].Message.Content;

            if (!testOnly)
            {
                await UpdateContentfulEntry(cuteContentGenerateEntry, entry, promptResult, displayActions);
                queryResult.Remove(entry);
            }
        }
    }

    private async Task SaveCuteBatchEntry(CuteContentGenerateBatch batchEntry, DisplayActions displayActions)
    {
        var entry = batchEntry.ToEntry(_contentLocales!.DefaultLocale);

        var latestEntry = await _contentfulConnection.GetManagementEntryAsync(
            entry.SystemProperties.Id,
            errorNotifier: (m) => displayActions.DisplayAlert?.Invoke(m.ToString().Snip(40))
        );

        await _contentfulConnection.CreateOrUpdateEntryAsync(
            entry.Fields,
            entry.SystemProperties.Id,
            latestEntry.SystemProperties.Version!.Value,
            errorNotifier: (m) => displayActions.DisplayAlert?.Invoke(m.ToString().Snip(40))
        );

        await _contentfulConnection.PublishEntryAsync(
            entry.SystemProperties.Id,
            latestEntry.SystemProperties.Version!.Value + 1,
            errorNotifier: (m) => displayActions.DisplayAlert?.Invoke(m.ToString().Snip(40))
        );
    }

    private async Task ListBatches(CuteContentGenerate cuteContentGenerateEntry, DisplayActions displayActions)
    {
        if (cuteContentGenerateEntry is null) return;

        displayActions.DisplayBlankLine?.Invoke();

        var azureOpenAiBatchProcessor = new AzureOpenAiBatchProcessor(_httpClient);

        displayActions.DisplayFormatted?.Invoke($"Getting batch status(es) from Azure Open AI at '{_httpClient.BaseAddress}'...");

        var response = await azureOpenAiBatchProcessor.BatchJobStatusList()
            ?? throw new CliException("List batch status(es) from Azure Open AI failed.");

        static DateTime? dd(int? i) => i is null ? null : DateTimeOffset.FromUnixTimeSeconds(i.Value).UtcDateTime;

        var batchEntries = CuteContentGenerateBatch
            .GetAll(_contentfulConnection)
            .Where(cb => cb.CuteContentGenerateEntry.Sys.Id == cuteContentGenerateEntry.Sys.Id)
            .ToDictionary(be => be.Key);

        displayActions.DisplayBlankLine?.Invoke();

        var batchNumber = response.Count;
        var listed = 0;

        foreach (var batchJobStatus in response.OrderByDescending(r => r.CreatedAt))
        {
            var uploadStatus = await azureOpenAiBatchProcessor.UploadStatus(batchJobStatus.InputFileId);

            if (uploadStatus is null)
            {
                continue;
            }

            var parts = uploadStatus.Filename.Split('-');
            var contentTypeSysId = parts[0];
            if (contentTypeSysId == "cute")
            {
                contentTypeSysId = $"cute-{parts[1]}";
            }

            if (!cuteContentGenerateEntry.Sys.Id.Equals(contentTypeSysId))
            {
                continue;
            }

            if (listed++ >= 5)
            {
                break;
            }

            var batchEntry = batchEntries.GetValueOrDefault(batchJobStatus.Id);

            displayActions.DisplayFormatted?.Invoke($"Batch number     : {batchNumber--:00000}");
            displayActions.DisplayFormatted?.Invoke($"Batch Id         : {batchJobStatus.Id}");
            displayActions.DisplayFormatted?.Invoke($"Status           : {batchJobStatus.Status}");
            displayActions.DisplayFormatted?.Invoke($"Created at       : {dd(batchJobStatus.CreatedAt):R}");
            displayActions.DisplayFormatted?.Invoke($"Completed at     : {dd(batchJobStatus.CompletedAt):R}");
            displayActions.DisplayFormatted?.Invoke($"Cancelled at     : {dd(batchJobStatus.CancelledAt):R}");
            displayActions.DisplayFormatted?.Invoke($"Failed at        : {dd(batchJobStatus.FailedAt):R}");
            displayActions.DisplayFormatted?.Invoke($"Expired at       : {dd(batchJobStatus.ExpiredAt):R}");
            displayActions.DisplayFormatted?.Invoke($"Output file      : {batchJobStatus.OutputFileId}");
            displayActions.DisplayFormatted?.Invoke($"Input file       : {batchJobStatus.InputFileId}");
            displayActions.DisplayFormatted?.Invoke($"Input file size  : {uploadStatus.Bytes / 1024:N0} Kb");
            displayActions.DisplayFormatted?.Invoke($"Cute key         : {cuteContentGenerateEntry.Key}");
            displayActions.DisplayFormatted?.Invoke($"Cute title       : {cuteContentGenerateEntry.Title}");
            if (batchEntry is null)
            {
                displayActions.DisplayRuler?.Invoke();
                continue;
            }
            displayActions.DisplayFormatted?.Invoke($"Content type     : {batchEntry.TargetContentType}");
            displayActions.DisplayFormatted?.Invoke($"Target Field     : {batchEntry.TargetField}");
            displayActions.DisplayFormatted?.Invoke($"Entries Count    : {batchEntry.TargetEntriesCount}");
            displayActions.DisplayFormatted?.Invoke($"Completion tokens: {batchEntry.CompletionTokens:N0}");
            displayActions.DisplayFormatted?.Invoke($"Prompt tokens    : {batchEntry.PromptTokens:N0}");
            displayActions.DisplayFormatted?.Invoke($"Total tokens     : {batchEntry.TotalTokens:N0}");
            displayActions.DisplayFormatted?.Invoke($"Overall status   : {batchEntry.Status}");

            displayActions.DisplayRuler?.Invoke();
        }
    }

    private async Task ProcessQueryResultsForModels(CuteContentGenerate cuteContentGenerateEntry, JArray queryResult,
        DisplayActions displayActions,
        Action<int, int>? progressUpdater,
        bool testOnly,
        string[] modelNames)
    {
        var first = true;

        foreach (var modelName in modelNames)
        {
            await ProcessQueryResults(cuteContentGenerateEntry, queryResult, displayActions, progressUpdater, testOnly, modelName, first);

            first = false;
        }
    }

    private async Task ProcessQueryResults(CuteContentGenerate cuteContentGenerateEntry, JArray queryResult,
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
            MaxTokens = cuteContentGenerateEntry.MaxTokenLimit,
            Temperature = (float)cuteContentGenerateEntry.Temperature,
            FrequencyPenalty = (float)cuteContentGenerateEntry.FrequencyPenalty,
            PresencePenalty = (float)cuteContentGenerateEntry.PresencePenalty,
            TopP = (float)cuteContentGenerateEntry.TopP
        };

        if (modelName is null)
        {
            modelName = cuteContentGenerateEntry.DeploymentModel;
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = null;
            }
        }

        var promptTemplate = Template.Parse(cuteContentGenerateEntry.Prompt);

        var systemTemplate = Template.Parse(cuteContentGenerateEntry.SystemMessage);

        var variableName = cuteContentGenerateEntry.CuteDataQueryEntry.VariablePrefix.Trim('.');

        var recordNum = 1;
        var recordTotal = queryResult.Count;

        foreach (var entry in queryResult.Cast<JObject>())
        {
            progressUpdater?.Invoke(recordNum++, recordTotal);

            if (EntryHasExistingContent(cuteContentGenerateEntry, entry) && !testOnly)
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
                await UpdateContentfulEntry(cuteContentGenerateEntry, entry, promptResult, displayActions);
            }

            scriptObject.Remove(variableName);
        }
    }

    private async Task ProcessQueryResultsInBatch(CuteContentGenerate cuteContentGenerateEntry, JArray queryResult, DisplayActions displayActions, Action<int, int>? progressUpdater, bool testOnly)
    {
        var batchStatus = await GetOpenBatchEntry(cuteContentGenerateEntry, queryResult, displayActions, progressUpdater, testOnly);

        if (batchStatus?.IsPending() ?? false)
        {
            displayActions.DisplayBlankLine?.Invoke();
            displayActions.DisplayFormatted?.Invoke($"The batch job submitted on {batchStatus.CreatedAt:O} is still in progress. ({batchStatus.Status})");
            return;
        }

        var scriptObject = CreateScriptObject();

        var promptTemplate = Template.Parse(cuteContentGenerateEntry.Prompt);

        var systemTemplate = Template.Parse(cuteContentGenerateEntry.SystemMessage);

        var variableName = cuteContentGenerateEntry.CuteDataQueryEntry.VariablePrefix.Trim('.');

        var recordNum = 1;

        var recordTotal = queryResult.Count;

        var jobs = new List<AzureOpenAiBatchRequest>();

        var options = _azureOpenAiOptionsProvider.GetAzureOpenAIClientOptions();

        foreach (var entry in queryResult.Cast<JObject>())
        {
            progressUpdater?.Invoke(recordNum++, recordTotal);

            if (EntryHasExistingContent(cuteContentGenerateEntry, entry) && !testOnly)
            {
                continue;
            }

            scriptObject.SetValue(variableName, entry, true);

            var job = new AzureOpenAiBatchRequest
            {
                CustomId = $"{cuteContentGenerateEntry.Sys.Id}|{entry.SelectToken("sys.id")}",
                Method = "POST",
                Url = "/chat/completions",
                Body = new BatchRequestBody
                {
                    Model = $"{options.DeploymentName}-batch",
                    Messages =
                    [
                        new ()
                        {
                            Role = "system",
                            Content = RenderTemplate(scriptObject, systemTemplate)
                        },
                        new ()
                        {
                            Role = "user",
                            Content = RenderTemplate(scriptObject, promptTemplate)
                        },
                    ]
                }
            };

            scriptObject.Remove(variableName);

            jobs.Add(job);
        };

        if (jobs.Count == 0)
        {
            displayActions.DisplayBlankLine?.Invoke();
            displayActions.DisplayFormatted?.Invoke($"No new content to generate...");
            return;
        }

        displayActions.DisplayBlankLine?.Invoke();

        var azureOpenAiBatchProcessor = new AzureOpenAiBatchProcessor(_httpClient);

        displayActions.DisplayFormatted?.Invoke($"Uploading to Azure Open AI at '{options.Endpoint}'...");
        displayActions.DisplayBlankLine?.Invoke();

        var response = await azureOpenAiBatchProcessor.UploadRequests(jobs)
            ?? throw new CliException("Batch file upload to Azure Open AI failed.");

        displayActions.DisplayFormatted?.Invoke($"File reference from Azure is '{response.Id}'...");
        displayActions.DisplayBlankLine?.Invoke();

        var completedResponse = await azureOpenAiBatchProcessor.WaitForUploadCompleted(response);

        displayActions.DisplayFormatted?.Invoke($"File upload completed for '{response.Id}' ({response.Bytes:N0} bytes)...");
        displayActions.DisplayBlankLine?.Invoke();

        displayActions.DisplayFormatted?.Invoke($"Creating batch job for cute batch '{azureOpenAiBatchProcessor.Id}'...");
        displayActions.DisplayBlankLine?.Invoke();

        var createBatchJobResponse = await azureOpenAiBatchProcessor.CreateBatchJob(completedResponse)
            ?? throw new CliException("Batch job creation on Azure Open AI failed.");

        displayActions.DisplayFormatted?.Invoke($"Created Azure batch '{createBatchJobResponse.Id}'...");
        displayActions.DisplayBlankLine?.Invoke();

        var batchJobStatus = await azureOpenAiBatchProcessor.BatchJobStatus(createBatchJobResponse)
            ?? throw new CliException("Batch job status failed on Azure Open AI.");

        displayActions.DisplayFormatted?.Invoke($"Azure batch '{createBatchJobResponse.Id}' has status of '{batchJobStatus.Status}'...");
        displayActions.DisplayBlankLine?.Invoke();

        var cuteBatchContentType = CuteContentGenerateBatchContentType.Instance();

        var serializer = new EntrySerializer(cuteBatchContentType, _contentLocales!);

        var batchFlatEntry = serializer.CreateNewFlatEntry();

        var createdDate = DateTimeOffset.FromUnixTimeSeconds(createBatchJobResponse.CreatedAt).UtcDateTime;

        batchFlatEntry["key.en"] = createBatchJobResponse.Id;
        batchFlatEntry["title.en"] = $"{cuteContentGenerateEntry.Title} - {createdDate:O}";
        batchFlatEntry["cuteContentGenerateEntry.en"] = cuteContentGenerateEntry.Sys.Id;
        batchFlatEntry["status.en"] = createBatchJobResponse.Status;
        batchFlatEntry["createdAt.en"] = createdDate;
        batchFlatEntry["targetContentType.en"] = GraphQLUtilities.GetContentTypeId(cuteContentGenerateEntry.CuteDataQueryEntry.Query);
        batchFlatEntry["targetField.en"] = cuteContentGenerateEntry.PromptOutputContentField;
        batchFlatEntry["targetEntriesCount.en"] = jobs.Count;

        var batchEntry = serializer.DeserializeEntry(batchFlatEntry);

        batchEntry.SystemProperties.ContentType = cuteBatchContentType;

        await _contentfulConnection.CreateOrUpdateEntryAsync(
            batchEntry.Fields,
            batchEntry.SystemProperties.Id, version: 0,
            errorNotifier: (m) => displayActions.DisplayAlert?.Invoke(m.ToString().Snip(40))
        );

        await _contentfulConnection.PublishEntryAsync(
            batchEntry.SystemProperties.Id,
            version: 1,
            errorNotifier: (m) => displayActions.DisplayAlert?.Invoke(m.ToString().Snip(40))
        );

        displayActions.DisplayFormatted?.Invoke($"Created {"cuteContentGenerateBatch"} entry to track batch progress.");
        displayActions.DisplayBlankLine?.Invoke();
    }

    private void ProcessQueryResultsInParallel(CuteContentGenerate cuteContentGenerateEntry, JArray queryResult,
            DisplayActions displayActions,
            Action<int, int>? progressUpdater,
            bool testOnly,
            string? modelName = null,
            bool displaySystemMessageAndPrompt = true)
    {
        var scriptObject = CreateScriptObject();

        var promptTemplate = Template.Parse(cuteContentGenerateEntry.Prompt);

        var systemTemplate = Template.Parse(cuteContentGenerateEntry.SystemMessage);

        var variableName = cuteContentGenerateEntry.CuteDataQueryEntry.VariablePrefix.Trim('.');

        var recordNum = 1;

        var recordTotal = queryResult.Count;

        var jobs = new List<GenerateJob>();

        foreach (var entry in queryResult.Cast<JObject>())
        {
            progressUpdater?.Invoke(recordNum++, recordTotal);

            if (EntryHasExistingContent(cuteContentGenerateEntry, entry) && !testOnly)
            {
                continue;
            }

            scriptObject.SetValue(variableName, entry, true);

            var job = new GenerateJob
            {
                EntryKey = entry["key"]?.Value<string>()
                    ?? "(unknown entry key)",

                EntryTitle = entry["title"]?.Value<string>()
                    ?? entry["name"]?.Value<string>()
                    ?? "(unknown entry title)",

                SystemMessage = RenderTemplate(scriptObject, systemTemplate),

                Prompt = RenderTemplate(scriptObject, promptTemplate),

                Entry = entry
            };

            scriptObject.Remove(variableName);

            jobs.Add(job);
        }

        var chatClient = CreateChatClient(modelName, displayActions);

        var chatCompletionOptions = new ChatCompletionOptions()
        {
            MaxTokens = cuteContentGenerateEntry.MaxTokenLimit,
            Temperature = (float)cuteContentGenerateEntry.Temperature,
            FrequencyPenalty = (float)cuteContentGenerateEntry.FrequencyPenalty,
            PresencePenalty = (float)cuteContentGenerateEntry.PresencePenalty,
            TopP = (float)cuteContentGenerateEntry.TopP
        };

        if (modelName is null)
        {
            modelName = cuteContentGenerateEntry.DeploymentModel;
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = null;
            }
        }

        var modelNameAsString = modelName == null ? string.Empty : $"[{modelName}] ";

        var jobNum = 1;

        var jobTotal = jobs.Count;

        var taskList = new List<GenerateJob>();

        var lastJob = jobs.LastOrDefault();

        foreach (var job in jobs)
        {
            progressUpdater?.Invoke(jobNum++, jobTotal);

            job.GenerateTask = SendPromptToModel(chatClient, chatCompletionOptions, job.SystemMessage, job.Prompt);

            taskList.Add(job);

            if (taskList.Count < 25 && job != lastJob) continue;

            displayActions.DisplayBlankLine?.Invoke();

            displayActions.DisplayFormatted?.Invoke($"Generating content for {taskList.Count} entries...");

            Task.WaitAll(taskList.Select(j => j.GenerateTask).ToArray());

            displayActions.DisplayBlankLine?.Invoke();

            foreach (var completedJob in taskList)
            {
                displayActions.DisplayAlert?.Invoke($"[{completedJob.EntryKey}] : [{completedJob.EntryTitle}]");
                displayActions.DisplayBlankLine?.Invoke();

                displayActions.DisplayRuler?.Invoke();

                if (displaySystemMessageAndPrompt)
                {
                    displayActions.DisplayHeading?.Invoke("System Message:");

                    DisplayLines(completedJob.SystemMessage, displayActions.DisplayDim);

                    displayActions.DisplayBlankLine?.Invoke();

                    displayActions.DisplayHeading?.Invoke("Prompt:");

                    DisplayLines(completedJob.Prompt, displayActions.DisplayDim);
                }

                displayActions.DisplayBlankLine?.Invoke();

                displayActions.DisplayHeading?.Invoke($"{modelNameAsString}Response:");

                var promptResult = FixFormatting(completedJob.GenerateTask.Result);

                DisplayLines(promptResult, displayActions.DisplayNormal);

                displayActions.DisplayBlankLine?.Invoke();

                if (!testOnly)
                {
                    completedJob.UpdateTask = UpdateContentfulEntry(cuteContentGenerateEntry, completedJob.Entry, promptResult, displayActions);
                }
            }

            displayActions.DisplayBlankLine?.Invoke();

            displayActions.DisplayFormatted?.Invoke($"Saving {taskList.Count} entries...");

            Task.WaitAll(taskList.Select(j => j.UpdateTask).ToArray());

            displayActions.DisplayBlankLine?.Invoke();

            taskList.Clear();
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

    private static bool EntryHasExistingContent(CuteContentGenerate cuteContentGenerateEntry, JObject entry)
    {
        var content = entry.SelectToken(cuteContentGenerateEntry.PromptOutputContentField);

        if (content.IsNull()) return false;

        if (content is JArray jArray)
        {
            return jArray.Count > 0;
        }

        if (content is JObject jObj)
        {
            return jObj.HasValues;
        }

        if (string.IsNullOrWhiteSpace(content?.Value<string>())) return false;

        return true;
    }

    private async Task UpdateContentfulEntry(CuteContentGenerate cuteContentGenerateEntry, JObject entry, string promptResult, DisplayActions displayActions)
    {
        var id = entry.SelectToken("$.sys.id")?.Value<string>() ??
            throw new CliException("The query needs to return a 'sys.id' for each item.");

        var obj = await _contentfulConnection.GetManagementEntryAsync(id,
            errorNotifier: (m) => displayActions.DisplayAlert?.Invoke(m.ToString().Snip(40)));

        var fields = obj.Fields as JObject ??
            throw new CliException("Weird! The entry does not have any fields?? I'd run without looking back..");

        var contentTypeId = obj.SystemProperties.ContentType.SystemProperties.Id;

        var contentType = (_withContentTypes?[contentTypeId])
            ?? throw new CliException($"The content type '{contentTypeId}' was not resolved. Did you call '.WithContentTypes' method first?");

        var fieldId = cuteContentGenerateEntry.PromptOutputContentField;

        var fieldDefinition = contentType.Fields.FirstOrDefault(f => f.Id == fieldId)
            ?? throw new CliException($"The field '{fieldId}' was not found in the content type '{contentTypeId}'.");

        var oldValueRef = fields[fieldId];

        var locale = cuteContentGenerateEntry.GeneratorTargetDataLanguageEntry?.Iso2code ?? _contentLocales?.DefaultLocale ?? "en";

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

        JToken? replaceValue = fieldDefinition.Type switch
        {
            "Symbol" or "Text" => promptResult,
            "Object" => ToObject(promptResult, displayActions),
            "RichText" => ToRichText(promptResult),
            "Array" => ToArray(promptResult, fieldDefinition.Items, fieldId, fieldDefinition),
            _ => throw new CliException($"Field '{fieldId}' is of type '{fieldDefinition.Type}' which can't store prompt results."),
        };

        if (replaceValue is null)
        {
            _displayAction?.Invoke($"The AI result for entry '{id}' could not be converted to '{fieldDefinition.Type}' for field '{fieldId}'. ('{promptResult}')");
            return;
        }

        oldValueRef[locale] = replaceValue;

        await _contentfulConnection.CreateOrUpdateEntryAsync(obj, obj.SystemProperties.Version,
            errorNotifier: (m) => displayActions?.DisplayAlert?.Invoke(m.ToString().Snip(40))
        );
    }

    private static JToken? ToObject(string promptResult, DisplayActions displayActions)
    {
        try
        {
            return JsonConvert.DeserializeObject<JToken>(promptResult);
        }
        catch (Exception ex)
        {
            displayActions.DisplayBlankLine?.Invoke();
            displayActions.DisplayRuler?.Invoke();
            displayActions.DisplayAlert?.Invoke(ex.Message);
            displayActions.DisplayBlankLine?.Invoke();
            DisplayLines(promptResult, displayActions.DisplayAlert);
            displayActions.DisplayRuler?.Invoke();
            displayActions.DisplayBlankLine?.Invoke();
        }
        return null;
    }

    private static JArray ToArray(string promptResult, Schema items, string fieldId, Field fieldDefinition)
    {
        return items.Type switch
        {
            "Symbol" or "Text" => ToFormattedStringArray(promptResult),
            "Object" => ToFormattedStringArray(promptResult),
            _ => throw new CliException($"Field '{fieldId}' is of type '{fieldDefinition.Type}' which can't store prompt results."),
        };
    }

    private static JArray ToFormattedStringArray(string promptResult)
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

        CuteFunctions.ContentfulConnection = _contentfulConnection;

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

    private async Task<JArray?> GetQueryData(CuteContentGenerate cuteContentGenerateEntry)
    {
        // Add the target field to the query if it doesn't exist...
        var query = GraphQLUtilities.EnsureFieldExistsOrAdd(cuteContentGenerateEntry.CuteDataQueryEntry.Query, cuteContentGenerateEntry.PromptOutputContentField);

        return await _graphQlClient.GetData(
            query,
            cuteContentGenerateEntry.CuteDataQueryEntry.JsonSelector,
            cuteContentGenerateEntry.GeneratorTargetDataLanguageEntry?.Iso2code ?? _contentLocales?.DefaultLocale ?? "en",
            preview: true);
    }

    private static string RenderTemplate(ScriptObject scriptObject, Template template)
    {
        return template.Render(scriptObject, memberRenamer: member => member.Name.ToCamelCase());
    }

    private class GenerateJob
    {
        public string EntryKey { get; init; } = default!;
        public string EntryTitle { get; init; } = default!;
        public string SystemMessage { get; init; } = default!;
        public string Prompt { get; init; } = default!;
        public JObject Entry { get; init; } = default!;
        public Task<string> GenerateTask { get; internal set; } = default!;
        public Task UpdateTask { get; internal set; } = default!;
    }
}