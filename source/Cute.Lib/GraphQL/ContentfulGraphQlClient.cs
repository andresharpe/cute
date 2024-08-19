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

    public async Task<JArray?> GetData(string query, string jsonResultsPath, string locale)
    {
        var postBody = JsonConvert.SerializeObject(new
        {
            query,
            variables = new Dictionary<string, object>()
            {
                ["preview"] = false,
                ["locale"] = locale,
            }
        });

        var request = new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            Content = new StringContent(postBody, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();

        var responseObject = JsonConvert.DeserializeObject<JObject>(responseString);

        if (responseObject is null) return null;

        return responseObject.SelectToken(jsonResultsPath) as JArray;
    }
}