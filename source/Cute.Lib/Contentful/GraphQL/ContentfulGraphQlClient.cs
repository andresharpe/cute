using Cute.Lib.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Cute.Lib.Contentful.GraphQL;

public class ContentfulGraphQlClient
{
    private const int MaxRetryCount = 10;
    private const int InitialRetryDelayMs = 1000;

    private static readonly ILogger Log = Serilog.Log.ForContext<ContentfulGraphQlClient>();

    private static readonly HashSet<HttpStatusCode> TransientStatusCodes =
    [
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.GatewayTimeout,
    ];

    private readonly HttpClient _httpClient;
    private readonly Uri _baseAddress;
    private readonly string _managementApiLey;
    private readonly string _deliveryApiKey;
    private readonly string _previewApiKey;
    private readonly ContentfulConnection _contentfulConnection;

    public ContentfulGraphQlClient(
        ContentfulConnection contentfulConnection,
        HttpClient httpClient)
    {
        _httpClient = httpClient;

        var env = contentfulConnection.Options.Environment;

        var space = contentfulConnection.Options.SpaceId;

        _baseAddress = new Uri($"https://graphql.contentful.com/content/v1/spaces/{space}/environments/{env}");

        _managementApiLey = contentfulConnection.Options.ManagementApiKey;

        _deliveryApiKey = contentfulConnection.Options.DeliveryApiKey;

        _previewApiKey = contentfulConnection.Options.PreviewApiKey;

        // recursive link! tried to avoid this but it's the only way to make AutoGraphqlQueryBuilder work
        _contentfulConnection = contentfulConnection;
    }

    public AutoGraphQlQueryBuilder CreateAutoQueryBuilder()
        => new AutoGraphQlQueryBuilder(_contentfulConnection);

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

        var retryCount = 0;
        while (true)
        {
            HttpRequestMessage CreateRequest()
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = _baseAddress,
                    Method = HttpMethod.Post,
                    Content = new StringContent(JsonConvert.SerializeObject(postBody), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                return request;
            }

            using var response = await ContentfulConnection.RateLimiter.SendRequestAsync(
                () => _httpClient.SendAsync(CreateRequest()),
                $"",
                (m) => { }, // suppress this message
                (e) => Log.Warning("GraphQL request error (will be retried by RateLimiter): {Error}", e.ToString())
            );

            if (TransientStatusCodes.Contains(response.StatusCode) && retryCount < MaxRetryCount)
            {
                retryCount++;
                Log.Warning("GraphQL transient HTTP {StatusCode}, retry {RetryCount}/{MaxRetry} (limit={Limit})",
                    (int)response.StatusCode, retryCount, MaxRetryCount, postBody.variables["limit"]);

                if (response.StatusCode == HttpStatusCode.BadGateway)
                {
                    postBody.variables["limit"] = Math.Max(1, (int)postBody.variables["limit"] / 2);
                }

                await Task.Delay(InitialRetryDelayMs * retryCount);
                continue;
            }

            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();

            var responseObject = JsonConvert.DeserializeObject<JObject>(responseString);

            if (responseObject is null)
                yield break;

            if (responseObject.SelectToken("errors") is JArray errors && errors.Count > 0)
            {
                if (retryCount < MaxRetryCount)
                {
                    retryCount++;
                    postBody.variables["limit"] = Math.Max(1, (int)postBody.variables["limit"] / 2);

                    var errorMsg = errors[0]["message"]?.ToString() ?? "Unknown";
                    Log.Warning("GraphQL error: {Error}, retry {RetryCount}/{MaxRetry} (limit={Limit})",
                        errorMsg, retryCount, MaxRetryCount, postBody.variables["limit"]);

                    await Task.Delay(InitialRetryDelayMs * retryCount);
                    continue;
                }
                var errorMessage = errors[0]["message"]?.ToString() ?? "Unknown GraphQL error";
                var errorCode = errors[0].SelectToken("extensions.contentful.code")?.ToString();
                Log.Error("GraphQL error after {MaxRetry} retries: {Error}", MaxRetryCount, errorMessage);
                throw new CliException($"GraphQL error: {errorMessage}" + (errorCode != null ? $" (Code: {errorCode})" : ""));
            }

            if (retryCount > 0)
            {
                Log.Debug("GraphQL request recovered after {RetryCount} retries", retryCount);
            }

            retryCount = 0;

            if (responseObject.SelectToken(jsonResultsPath) is not JArray newRecords)
                yield break;

            if (newRecords.Count == 0)
                yield break;

            foreach (var record in newRecords)
            {
                yield return (JObject)record;
                recCount++;
                if (limit is not null && recCount >= limit)
                    yield break;
            }

            if (newRecords.Count < (int)postBody.variables["limit"])
                yield break;

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

    public async IAsyncEnumerable<JObject> GetRawDataEnumerable(string query,
        string locale, int? limit = null, bool preview = false)
    {
        var apiKey = preview ? _previewApiKey : _deliveryApiKey;

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

        var retryCount = 0;
        while (true)
        {
            HttpRequestMessage CreateRequest()
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = _baseAddress,
                    Method = HttpMethod.Post,
                    Content = new StringContent(JsonConvert.SerializeObject(postBody), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                return request;
            }

            using var response = await ContentfulConnection.RateLimiter.SendRequestAsync(
                () => _httpClient.SendAsync(CreateRequest()),
                $"",
                (m) => { }, // suppress this message
                (e) => Log.Warning("Raw GraphQL request error (will be retried by RateLimiter): {Error}", e.ToString())
            );

            if (TransientStatusCodes.Contains(response.StatusCode) && retryCount < MaxRetryCount)
            {
                retryCount++;
                Log.Warning("Raw GraphQL transient HTTP {StatusCode}, retry {RetryCount}/{MaxRetry} (limit={Limit})",
                    (int)response.StatusCode, retryCount, MaxRetryCount, postBody.variables["limit"]);

                if (response.StatusCode == HttpStatusCode.BadGateway)
                {
                    postBody.variables["limit"] = Math.Max(1, (int)postBody.variables["limit"] / 2);
                }

                await Task.Delay(InitialRetryDelayMs * retryCount);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                Log.Error("Raw GraphQL request failed with HTTP {StatusCode}: {Message}",
                    (int)response.StatusCode, message);
                throw new CliException(message);
            }

            if (retryCount > 0)
            {
                Log.Debug("Raw GraphQL request recovered after {RetryCount} retries", retryCount);
            }

            retryCount = 0;

            var responseString = await response.Content.ReadAsStringAsync();

            var responseObject = JsonConvert.DeserializeObject<JObject>(responseString);

            if (responseObject is null) yield break;

            yield return responseObject;

            if (responseObject.SelectToken("$.data.*.items") is not JArray items) yield break;

            if (items.Count < (int)postBody.variables["limit"]) yield break;

            postBody.variables["skip"] = (int)postBody.variables["skip"] + (int)postBody.variables["limit"];
        }
    }
}
