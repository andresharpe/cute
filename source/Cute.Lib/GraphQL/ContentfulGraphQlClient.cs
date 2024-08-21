using Cute.Lib.Contentful;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace Cute.Lib.GraphQL;

public class ContentfulGraphQlClient
{
    private readonly HttpClient _httpClient;

    public ContentfulGraphQlClient(
        ContentfulConnection contentfulConnection,
        ILogger<ContentfulGraphQlClient> logger,
        HttpClient httpClient)
    {
        _httpClient = httpClient;

        var env = contentfulConnection.Options.Environment;
        var space = contentfulConnection.Options.SpaceId;
        var apiKey = contentfulConnection.Options.DeliveryApiKey;

        _httpClient.BaseAddress = new Uri($"https://graphql.contentful.com/content/v1/spaces/{space}/environments/{env}");

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<JArray?> GetData(string query, string jsonResultsPath, string locale,
        int? limit = null)
    {
        var postBody = new
        {
            query,
            variables = new Dictionary<string, object>()
            {
                ["preview"] = false,
                ["locale"] = locale,
                ["skip"] = 0,
                ["limit"] = limit ?? 1000,
            }
        };

        JArray records = [];

        while (true)
        {
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                Content = new StringContent(JsonConvert.SerializeObject(postBody), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();

            var responseObject = JsonConvert.DeserializeObject<JObject>(responseString);

            if (responseObject is null) return null;

            if (responseObject.SelectToken(jsonResultsPath) is not JArray newRecords) return records;

            if (newRecords.Count == 0) break;

            records.Merge(newRecords);

            if (limit is not null && records.Count >= limit) break;

            if (newRecords.Count < (int)postBody.variables["limit"]) break;

            postBody.variables["skip"] = (int)postBody.variables["skip"] + (int)postBody.variables["limit"];

            await Task.Delay(100);
        }

        return records;
    }
}