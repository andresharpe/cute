namespace Cute.Lib.AzureOpenAi.Batch;

using Cute.Lib.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

public class AzureOpenAiBatchProcessor(HttpClient httpClient)
{
    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        },
        Formatting = Formatting.None
    };

    private readonly HttpClient _httpClient = httpClient;
    private readonly object _batchApiVersion = "2024-07-01-preview";

    public Guid Id { get; set; } = Guid.NewGuid();

    public async Task<BatchFileUploadResponse?> UploadRequests(IReadOnlyList<AzureOpenAiBatchRequest> batchRequests)
    {
        if (batchRequests.Count == 0)
        {
            return null;
        }

        using var requestContent = new MultipartFormDataContent
        {
            {
                new StringContent("batch"),
                "purpose"
            },
            {
                new StringContent(
                    ToJsonLString(batchRequests),Encoding.UTF8,
                    MediaTypeHeaderValue.Parse("application/json")
                ),
                "file",
                $"{batchRequests[0].CustomId.Split('|')[0]}-{Id}.jsonl"
            }
        };

        using var response = await _httpClient.PostAsync($"/openai/files?api-version={_batchApiVersion}",
            requestContent);

        response.EnsureSuccessStatusCode();

        var stringResponse = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<BatchFileUploadResponse>(stringResponse, _jsonSettings);
    }

    public async Task<BatchFileUploadResponse?> UploadStatus(BatchFileUploadResponse batchFileUploadResponse)
    {
        return await UploadStatus(batchFileUploadResponse.Id);
    }

    public async Task<BatchFileUploadResponse?> UploadStatus(string batchFileUploadResponseId)
    {
        using var response = await _httpClient.GetAsync($"/openai/files/{batchFileUploadResponseId}?api-version={_batchApiVersion}");

        response.EnsureSuccessStatusCode();

        var stringResponse = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<BatchFileUploadResponse>(stringResponse, _jsonSettings);
    }

    public async Task<BatchFileUploadResponse> WaitForUploadCompleted(BatchFileUploadResponse batchFileUploadResponse)
    {
        ArgumentNullException.ThrowIfNull(batchFileUploadResponse);

        while (!batchFileUploadResponse.Status.Equals("processed"))
        {
            await Task.Delay(1000);
            batchFileUploadResponse = await UploadStatus(batchFileUploadResponse) ??
                throw new CliException($"Error calling status check for natch upload job '{Id}'.");
        }
        return batchFileUploadResponse;
    }

    public async Task<CreateBatchJobResponse?> CreateBatchJob(BatchFileUploadResponse batchFileUploadResponse)
    {
        using var response = await _httpClient.PostAsync($"/openai/batches?api-version={_batchApiVersion}",
            new StringContent(JsonConvert.SerializeObject(new
            {
                InputFileId = batchFileUploadResponse.Id,
                Endpoint = "/chat/completions",
                CompletionWindow = "24h"
            }, _jsonSettings
            ), Encoding.UTF8, "application/json")
        );

        response.EnsureSuccessStatusCode();

        var stringResponse = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<CreateBatchJobResponse>(stringResponse, _jsonSettings);
    }

    public async Task<CreateBatchJobResponse?> BatchJobStatus(CreateBatchJobResponse createBatchJobResponse)
    {
        return await BatchJobStatus(createBatchJobResponse.Id);
    }

    public async Task<CreateBatchJobResponse?> BatchJobStatus(string createBatchJobResponseId)
    {
        using var response = await _httpClient.GetAsync($"/openai/batches/{createBatchJobResponseId}?api-version={_batchApiVersion}");

        response.EnsureSuccessStatusCode();

        var stringResponse = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<CreateBatchJobResponse>(stringResponse, _jsonSettings);
    }

    public async Task<CreateBatchJobResponse?> BatchJobCancel(CreateBatchJobResponse createBatchJobResponse)
    {
        using var response = await _httpClient.PostAsync($"/openai/batches/{createBatchJobResponse.Id}/cancel?api-version={_batchApiVersion}",
            new StringContent("", Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();

        var stringResponse = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<CreateBatchJobResponse>(stringResponse, _jsonSettings);
    }

    public IAsyncEnumerable<BatchJobResultResponse> BatchJobResult(CreateBatchJobResponse createBatchJobResponse)
    {
        return BatchJobResult(createBatchJobResponse.OutputFileId);
    }

    public async IAsyncEnumerable<BatchJobResultResponse> BatchJobResult(string? outputFileId)
    {
        if (outputFileId == null)
        {
            yield break;
        }

        using var response = await _httpClient.GetAsync($"/openai/files/{outputFileId}/content?api-version={_batchApiVersion}",
                HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var streamReader = new StreamReader(stream);

        string? line;
        while ((line = await streamReader.ReadLineAsync()) != null)
        {
            var obj = JsonConvert.DeserializeObject<BatchJobResultResponse>(line, _jsonSettings);
            if (obj != null)
            {
                yield return obj;
            }
        }
    }

    public async Task<IReadOnlyList<CreateBatchJobResponse>?> BatchJobStatusList()
    {
        using var response = await _httpClient.GetAsync($"/openai/batches?api-version={_batchApiVersion}");

        response.EnsureSuccessStatusCode();

        var stringResponse = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<BatchJobStatusListResponse>(stringResponse, _jsonSettings)?.Data;
    }

    private static string ToJsonLString(IEnumerable<AzureOpenAiBatchRequest> batchRequests)
    {
        var jsonlContent = new StringBuilder();
        foreach (var batchRequest in batchRequests)
        {
            jsonlContent.AppendLine(JsonConvert.SerializeObject(batchRequest, _jsonSettings));
        }
        return jsonlContent.ToString();
    }
}