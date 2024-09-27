namespace Cute.Unit.Tests;

using Cute.Config;
using Cute.Constants;
using Cute.Lib.AzureOpenAi.Batch;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

public class GenerateBulkActionTest
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    private readonly AppSettings _appSettings;
    private readonly ContentfulConnection _contentfulConnection;
    private readonly string _openAiEndpoint;

    private readonly string _openAiApiKey;

    private readonly HttpClient _httpClient;

    public GenerateBulkActionTest()
    {
        _dataProtectionProvider = DataProtectionProvider.Create(Globals.AppName);

        _appSettings = new PersistedTokenCache(_dataProtectionProvider)
            .LoadAsync(Globals.AppName)
            .Result!;

        _contentfulConnection = new ContentfulConnection(new HttpClient(), _appSettings);

        _openAiEndpoint = _appSettings.OpenAiEndpoint;

        _openAiApiKey = _appSettings.OpenAiApiKey;

        _httpClient = new()
        {
            BaseAddress = new Uri(_openAiEndpoint)
        };

        _httpClient.DefaultRequestHeaders.Add("api-key", _openAiApiKey);
    }

    [Fact]
    public async Task Test_CreateAndCancelBatch()
    {
        var azureOpenAiBatchProcessor = new AzureOpenAiBatchProcessor(_httpClient);

        List<AzureOpenAiBatchRequest> batchRequests = DefaultBatchRequest();

        var response = await azureOpenAiBatchProcessor.UploadRequests(batchRequests);

        response.Should().NotBeNull();
        response!.Status.Should().Be("pending");

        var completedResponse = await azureOpenAiBatchProcessor.WaitForUploadCompleted(response);

        completedResponse.Should().NotBeNull();
        completedResponse!.Status.Should().Be("processed");

        var createBatchJobResponse = await azureOpenAiBatchProcessor.CreateBatchJob(completedResponse);
        createBatchJobResponse.Should().NotBeNull();
        createBatchJobResponse!.Status.Should().NotBeNullOrEmpty();

        var cancelBatchJobResponse = await azureOpenAiBatchProcessor.BatchJobCancel(createBatchJobResponse);
        cancelBatchJobResponse.Should().NotBeNull();
        cancelBatchJobResponse!.Status.Should().Be("cancelling");
    }

    [Fact]
    public async Task Test_GetBatchJobStatusList()
    {
        var azureOpenAiBatchProcessor = new AzureOpenAiBatchProcessor(_httpClient);

        var batchJobStatusList = await azureOpenAiBatchProcessor.BatchJobStatusList();
        batchJobStatusList.Should().NotBeNull();
        batchJobStatusList!.Count.Should().BeGreaterThan(0);

        var batchJobStatus = batchJobStatusList![0];
        batchJobStatus.Should().NotBeNull();
        batchJobStatus.Status.Should().Be("completed");

        await foreach (var batchJobResult in azureOpenAiBatchProcessor.BatchJobResult(batchJobStatus))
        {
            batchJobResult.Should().NotBeNull();
            batchJobResult.Response.StatusCode.Should().Be(200);
            batchJobResult.Response.Should().NotBeNull();
            batchJobResult.Response.Body.Should().NotBeNull();
            batchJobResult.Response.Body.Choices.Should().NotBeNull();
            batchJobResult.Response.Body.Choices.Should().HaveCount(1);
            batchJobResult.Response.Body.Choices[0].Message.Should().NotBeNull();
            batchJobResult.Response.Body.Choices[0].Message.Content.Should().NotBeNull();

            var data = batchJobResult.Response.Body.Choices[0].Message.Content;
            data.Should().NotBeNullOrEmpty();

            var entryInfo = batchJobResult.CustomId.Split('|');
            entryInfo.Should().HaveCount(2);

            var cuteContentGenerateEntryId = entryInfo[0];
            var targetEntryId = entryInfo[1];

            cuteContentGenerateEntryId.Should().NotBeNullOrEmpty();
            targetEntryId.Should().NotBeNullOrEmpty();

            var cuteContentGenerateEntry = await _contentfulConnection
                .GetPreviewEntryAsync<CuteContentGenerate>(cuteContentGenerateEntryId);

            cuteContentGenerateEntry.Should().NotBeNull();

            var targetEntry = await _contentfulConnection
                .GetManagementEntryAsync(targetEntryId);

            targetEntry.Should().NotBeNull();

            var jObj = targetEntry.Fields as JObject;
            jObj?[cuteContentGenerateEntry!.PromptOutputContentField]?.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Test_UploadRequests()
    {
        var azureOpenAiBatchProcessor = new AzureOpenAiBatchProcessor(_httpClient);

        List<AzureOpenAiBatchRequest> batchRequests = DefaultBatchRequest();

        var response = await azureOpenAiBatchProcessor.UploadRequests(batchRequests);

        response.Should().NotBeNull();
        response!.Status.Should().Be("pending");

        var completedResponse = await azureOpenAiBatchProcessor.WaitForUploadCompleted(response);

        completedResponse.Should().NotBeNull();
        completedResponse!.Status.Should().Be("processed");

        var createBatchJobResponse = await azureOpenAiBatchProcessor.CreateBatchJob(completedResponse);
        createBatchJobResponse.Should().NotBeNull();
        createBatchJobResponse!.Status.Should().NotBeNullOrEmpty();

        var batchJobStatus = await azureOpenAiBatchProcessor.BatchJobStatus(createBatchJobResponse);
        batchJobStatus.Should().NotBeNull();
        batchJobStatus!.Status.Should().NotBeNullOrEmpty();
    }

    private List<AzureOpenAiBatchRequest> DefaultBatchRequest()
    {
        return
        [
            new ()
            {
                CustomId = "7PVgVpOGO9PGqkIOB7t2y|cute-777KQty1LAJCFbb0PDkdBg",
                Method = "POST",
                Url = "/chat/completions",
                Body = new BatchRequestBody
                {
                    Model = $"{_appSettings.OpenAiDeploymentName}-batch",
                    Messages =
                    [
                        new ()
                        {
                            Role = "system",
                            Content = """
                            You are a professional researcher working for a company that sells flexible workspace,
                            offices, coworking, meeting rooms, virtual offices and other services designed to provide
                            businesses with flexibility and to reduce their costs.

                            You are researching possible areas for serviced offices, coworking, and meeting room locations.
                            """
                        },
                        new ()
                        {
                            Role = "user",
                            Content = """
                            List prominent addresses, streets, neighborhoods, business parks, motorway/highway offramps and
                            transit points that are well-known for office space, coworking, and meeting rooms in Johannesburg, Gauteng.

                            Adhere to the following rules:
                            - Rank by the most important locations
                            - List between 16 and 24 entries.
                            - Include the latitude/longitude for each result.
                            - Exclude residential areas, care homes, or locations primarily used for non-business purposes.
                            - Exclude specific building names and numbers.
                            - Do not add context, introductions, markdown, or quotation marks to the output.
                            - Use JSON format with a single array of objects with keys "rank", "name", "lat", "lon".
                            - Each object starts on a new line and only takes up one line with a trailing comma where needed
                            """
                        },
                    ]
                }
            }
        ];
    }
}