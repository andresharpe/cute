﻿using Cute.Lib.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace Cute.Lib.Contentful.GraphQL;

public class ContentfulGraphQlClient
{
    private readonly HttpClient _httpClient;
    private readonly Uri _baseAddress;
    private readonly string _deliveryApiKey;
    private readonly string _previewApiKey;

    public ContentfulGraphQlClient(
        ContentfulConnection contentfulConnection,
        HttpClient httpClient)
    {
        _httpClient = httpClient;

        var env = contentfulConnection.Options.Environment;

        var space = contentfulConnection.Options.SpaceId;

        _baseAddress = new Uri($"https://graphql.contentful.com/content/v1/spaces/{space}/environments/{env}");

        _deliveryApiKey = contentfulConnection.Options.DeliveryApiKey;

        _previewApiKey = contentfulConnection.Options.PreviewApiKey;
    }

    public async IAsyncEnumerable<JObject> GetDataEnumerable(string query,
        string jsonResultsPath, string locale,
        int? limit = null, bool preview = false)
    {
        var apiKey = preview ? _previewApiKey : _deliveryApiKey;

        var recCount = 0;

        var postBody = new
        {
            query,
            variables = new Dictionary<string, object>()
            {
                ["preview"] = preview,
                ["locale"] = locale,
                ["skip"] = 0,
                ["limit"] = limit ?? 1000,
            }
        };

        while (true)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = _baseAddress,
                Method = HttpMethod.Post,
                Content = new StringContent(JsonConvert.SerializeObject(postBody), Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await ContentfulConnection.RateLimiter.SendRequestAsync(
                () => _httpClient.SendAsync(request),
                $"",
                (m) => { }, // suppress this message
                (e) => throw new CliException(e.ToString())
            );

            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();

            var responseObject = JsonConvert.DeserializeObject<JObject>(responseString);

            if (responseObject is null) yield break;

            if (responseObject.SelectToken(jsonResultsPath) is not JArray newRecords)
                yield break;

            if (newRecords.Count == 0) yield break;

            foreach (var record in newRecords)
            {
                yield return (JObject)record;
                recCount++;
                if (limit is not null && recCount >= limit) yield break;
            }

            if (newRecords.Count < (int)postBody.variables["limit"]) yield break;

            postBody.variables["skip"] = (int)postBody.variables["skip"] + (int)postBody.variables["limit"];
        }
    }

    public async Task<JArray?> GetAllData(string query, string jsonResultsPath, string locale,
            int? limit = null, bool preview = false)
    {
        await Task.Delay(0);

        var enumerable = GetDataEnumerable(query, jsonResultsPath, locale, limit, preview)
            .ToBlockingEnumerable();

        return new JArray(enumerable);
    }
}