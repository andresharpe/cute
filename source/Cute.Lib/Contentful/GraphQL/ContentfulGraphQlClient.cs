using Cute.Lib.Exceptions;
using Cute.Lib.RateLimiters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace Cute.Lib.Contentful.GraphQL;

public class ContentfulGraphQlClient
{
    private readonly HttpClient _httpClient;

    private readonly ContentfulConnection _contentfulConnection;

    public ContentfulGraphQlClient(
        ContentfulConnection contentfulConnection,
        HttpClient httpClient)
    {
        _httpClient = httpClient;

        var env = contentfulConnection.Options.Environment;
        var space = contentfulConnection.Options.SpaceId;

        _httpClient.BaseAddress = new Uri($"https://graphql.contentful.com/content/v1/spaces/{space}/environments/{env}");

        _contentfulConnection = contentfulConnection;
    }

    public async Task<JArray?> GetData(string query, string jsonResultsPath, string locale,
        int? limit = null, bool preview = false)
    {
        var apiKey = preview ? _contentfulConnection.Options.PreviewApiKey : _contentfulConnection.Options.DeliveryApiKey;

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

        JArray records = [];

        while (true)
        {
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                Content = new StringContent(JsonConvert.SerializeObject(postBody), Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await RateLimiter.SendRequestAsync(
                () => _httpClient.SendAsync(request),
                $"",
                (m) => { }, // suppress this message
                (e) => throw new CliException(e.ToString())
            );

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
        }

        return records;
    }
}