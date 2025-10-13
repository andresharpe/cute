using Contentful.Core.Models;
using Cute.Lib.Cache;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.InputAdapters.Base;
using Cute.Lib.InputAdapters.Http.Models;
using Cute.Lib.InputAdapters.MemoryAdapters;
using Cute.Lib.Serializers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scriban;
using System.IO.Hashing;
using System.Text;

namespace Cute.Lib.InputAdapters.Http;

/// <summary>
/// Streaming version of HttpInputAdapter that processes large HTTP responses in memory-efficient batches
/// </summary>
public class StreamingHttpInputAdapter(
    HttpDataAdapterConfig adapter,
    ContentfulConnection contentfulConnection,
    ContentLocales contentLocales,
    IReadOnlyDictionary<string, string?> envSettings,
    IEnumerable<ContentType> contentTypes,
    HttpClient httpClient)
    : StreamingMappedInputAdapterBase(adapter.EndPoint, adapter, contentfulConnection, contentLocales, envSettings, contentTypes)
{
    private readonly HttpDataAdapterConfig _adapter = adapter;
    private readonly HttpClient _httpClient = httpClient;
    private HttpResponseFileCache? _httpResponseFileCache;

    private int _estimatedCount = -1;

    public StreamingHttpInputAdapter WithHttpResponseFileCache(HttpResponseFileCache? httpResponseFileCache)
    {
        _httpResponseFileCache = httpResponseFileCache;
        return this;
    }

    public override async Task<int> GetEstimatedRecordCountAsync()
    {
        if (_estimatedCount != -1) return _estimatedCount;

        _contentType = _contentTypes.FirstOrDefault(ct => ct.SystemProperties.Id == _adapter.ContentType)
            ?? throw new CliException($"Content type '{_adapter.ContentType}' does not exist.");

        _serializer = new EntrySerializer(_contentType, _contentLocales);

        // For estimation, we can either:
        // 1. Make a small sample request if the API supports limit parameters
        // 2. Make the first request and estimate from pagination info
        // 3. Return a default estimate and adjust during processing

        try
        {
            // Try to get count from first page or sample
            if (_adapter.Pagination != null)
            {
                _estimatedCount = await EstimateCountFromPagination();
            }
            else
            {
                // For non-paginated APIs, make the full request to get accurate count
                // This is less memory efficient but necessary for accurate progress reporting
                _estimatedCount = await GetFullCountFromSingleRequest();
            }
        }
        catch (Exception ex)
        {
            ActionNotifier?.Invoke($"Warning: Could not estimate record count: {ex.Message}");
            _estimatedCount = 1000; // Default fallback estimate
        }

        return _estimatedCount;
    }

    public override async IAsyncEnumerable<IEnumerable<IDictionary<string, object?>>> GetRecordBatchesAsync(int batchSize = 1000)
    {
        await GetEstimatedRecordCountAsync(); // Ensure initialization

        if (_adapter.Pagination != null)
        {
            // For paginated APIs, yield each page as a batch
            await foreach (var batch in GetPaginatedBatchesAsync(batchSize))
            {
                yield return batch;
            }
        }
        else
        {
            // For single-request APIs, process the full response in chunks
            await foreach (var batch in GetSingleRequestBatchesAsync(batchSize))
            {
                yield return batch;
            }
        }
    }

    private async Task<int> EstimateCountFromPagination()
    {
        FormUrlEncodedContent? formContent = null;
        if (_adapter.FormUrlEncodedContent is not null)
        {
            formContent = new FormUrlEncodedContent(_adapter.FormUrlEncodedContent);
        }

        // Make a small sample request to estimate total
        var sampleLimit = Math.Min(100, _adapter.Pagination!.LimitMax);
        var sampleUri = BuildPaginatedUri(0, sampleLimit);
        
        var sampleResult = await GetResponseFromEndpointOrCache(formContent, sampleUri, 1);
        if (sampleResult?.ResponseContent == null) return 0;

        var sampleArray = ExtractResultArray(sampleResult.ResponseContent);
        
        // If we got fewer results than requested, that's likely the total
        if (sampleArray.Count < sampleLimit)
        {
            return sampleArray.Count;
        }

        // Otherwise, estimate based on pagination metadata or make additional requests
        // This is API-specific - some APIs return total counts in headers or response body
        
        // Fallback: assume at least 10 pages worth of data
        return sampleLimit * 10;
    }

    private async Task<int> GetFullCountFromSingleRequest()
    {
        FormUrlEncodedContent? formContent = null;
        if (_adapter.FormUrlEncodedContent is not null)
        {
            formContent = new FormUrlEncodedContent(_adapter.FormUrlEncodedContent);
        }

        var response = await MakeHttpCall(formContent);
        return response?.Count ?? 0;
    }

    private async IAsyncEnumerable<IEnumerable<IDictionary<string, object?>>> GetPaginatedBatchesAsync(int batchSize)
    {
        FormUrlEncodedContent? formContent = null;
        if (_adapter.FormUrlEncodedContent is not null)
        {
            formContent = new FormUrlEncodedContent(_adapter.FormUrlEncodedContent);
        }

        var skipTotal = 0;
        var requestCount = 0;

        while (true)
        {
            requestCount++;
            var requestUri = BuildPaginatedUri(skipTotal, _adapter.Pagination!.LimitMax);
            
            var results = await GetResponseFromEndpointOrCache(formContent, requestUri, requestCount);
            if (results?.ResponseContent == null) break;

            var rootArray = ExtractResultArray(results.ResponseContent);
            
            if (_adapter.FilterExpression is not null)
            {
                rootArray = FilterResultValues(rootArray);
            }

            ActionNotifier?.Invoke($"Processing page {requestCount} with {rootArray.Count} entries...");

            if (rootArray.Count == 0) break;

            // Convert JArray to flat entries and yield in batches
            var flatEntries = MapResultValues(rootArray).ToList();
            
            // Process flat entries in batches of the specified size
            for (int i = 0; i < flatEntries.Count; i += batchSize)
            {
                var batch = flatEntries.Skip(i).Take(batchSize)
                    .Select(dict => _serializer.CreateNewFlatEntry(dict))
                    .ToList();
                    
                yield return batch;
            }

            skipTotal += _adapter.Pagination.LimitMax;

            // Check if we've reached the end
            if (rootArray.Count < _adapter.Pagination.LimitMax)
            {
                break;
            }
        }
    }

    private async IAsyncEnumerable<IEnumerable<IDictionary<string, object?>>> GetSingleRequestBatchesAsync(int batchSize)
    {
        FormUrlEncodedContent? formContent = null;
        if (_adapter.FormUrlEncodedContent is not null)
        {
            formContent = new FormUrlEncodedContent(_adapter.FormUrlEncodedContent);
        }

        var uriDict = CompileValuesWithEnvironment(new Dictionary<string, string> { ["uri"] = _adapter.EndPoint });
        var results = await GetResponseFromEndpointOrCache(formContent, uriDict["uri"], 1);
        
        if (results?.ResponseContent == null) yield break;

        var rootArray = ExtractResultArray(results.ResponseContent);
        
        if (_adapter.FilterExpression is not null)
        {
            rootArray = FilterResultValues(rootArray);
        }

        ActionNotifier?.Invoke($"Processing {rootArray.Count} entries from single request...");

        // Process the array in streaming batches to avoid loading everything into memory
        for (int i = 0; i < rootArray.Count; i += batchSize)
        {
            var batchArray = new JArray(rootArray.Skip(i).Take(batchSize));
            var flatEntries = MapResultValues(batchArray)
                .Select(dict => _serializer.CreateNewFlatEntry(dict))
                .ToList();
                
            yield return flatEntries;

            CountProgressNotifier?.Invoke(Math.Min(i + batchSize, rootArray.Count), rootArray.Count, null);
        }
    }

    private string BuildPaginatedUri(int skip, int limit)
    {
        var uriDict = CompileValuesWithEnvironment(new Dictionary<string, string> { ["uri"] = _adapter.EndPoint });
        var baseUri = uriDict["uri"];
        
        return $"{baseUri}&{_adapter.Pagination!.SkipKey}={skip}&{_adapter.Pagination.LimitKey}={limit}";
    }

    private JArray ExtractResultArray(object responseContent)
    {
        if (_adapter.ResultsJsonPath is null)
        {
            return responseContent as JArray ?? new JArray(responseContent!)
                ?? throw new CliException("The result of the endpoint call is not a json array.");
        }
        else if (responseContent is JObject obj)
        {
            var selectedToken = obj.SelectToken($"$.{_adapter.ResultsJsonPath}");
            return selectedToken as JArray ?? new JArray(selectedToken!)
                ?? throw new CliException($"The json path '{_adapter.ResultsJsonPath}' does not exist or is not a json array.");
        }
        else
        {
            throw new CliException("The result of the endpoint call is not a valid json object or array.");
        }
    }

    private async Task<HttpResponseCacheEntry?> GetResponseFromEndpointOrCache(
        FormUrlEncodedContent? formContent, string requestUri, int requestCount)
    {
        Task<HttpResponseCacheEntry?> getEntryFunc() => GetResponseFromEndpoint(formContent, requestUri);

        if (_httpResponseFileCache is null) return await getEntryFunc();

        var uri = new Uri(requestUri, UriKind.Absolute);
        var fileBaseName = new StringBuilder()
            .Append(_adapter.Id).Append('_')
            .Append(uri.Host.Replace('.', '-').Trim('-')).Append('_')
            .Append($"{requestCount:D4}");

        if (formContent is not null)
        {
            var hashAlgo = new XxHash3();
            var item = await formContent.ReadAsByteArrayAsync();
            hashAlgo.Append(item);
            fileBaseName.Append('_').Append(hashAlgo.GetCurrentHashAsUInt64());
        }

        return await _httpResponseFileCache.Get(fileBaseName.ToString(), getEntryFunc);
    }

    private async Task<HttpResponseCacheEntry?> GetResponseFromEndpoint(
        FormUrlEncodedContent? formContent, string requestUri)
    {
        _httpClient.DefaultRequestHeaders.Clear();

        if (_adapter.Headers is not null)
        {
            var compiledHeaders = CompileValuesWithEnvironment(_adapter.Headers);
            foreach (var (key, value) in compiledHeaders)
            {
                _httpClient.DefaultRequestHeaders.Add(key, value);
            }
        }

        HttpResponseMessage? endpointResult = _adapter.HttpMethod switch
        {
            Models.HttpMethod.Post => await _httpClient.PostAsync(requestUri, formContent),
            Models.HttpMethod.Get => await _httpClient.GetAsync(requestUri),
            _ => throw new NotImplementedException($"Unknown method {_adapter.HttpMethod}")
        };

        endpointResult.EnsureSuccessStatusCode();

        string? optionalSecret = null;
        if (_adapter.ResultsSecret is not null)
        {
            var compiledSecret = CompileValuesWithEnvironment(new Dictionary<string, string> { ["secret"] = _adapter.ResultsSecret });
            optionalSecret = compiledSecret["secret"];
        }

        var endpointContent = _adapter.ResultsFormat switch
        {
            ResultsFormat.Json or ResultsFormat.Csv => await endpointResult.Content.ReadAsStringAsync(),
            ResultsFormat.ZippedCsv => await endpointResult.Content.ReadAndUnzipAsCsv(optionalSecret),
            _ => throw new NotImplementedException(),
        };

        if (endpointContent is null) return null;

        var results = _adapter.ResultsFormat switch
        {
            ResultsFormat.Json => JsonConvert.DeserializeObject(endpointContent),
            ResultsFormat.Csv or ResultsFormat.ZippedCsv => await GetCsvAsJarray(endpointContent),
            _ => throw new NotImplementedException(),
        };

        return new HttpResponseCacheEntry
        {
            RequestUri = requestUri,
            ResponseContent = results,
            ResponseContentHeaders = endpointResult.Headers
                .AsEnumerable()
                .ToDictionary(kv => kv.Key, kv => kv.Value.FirstOrDefault()),
        };
    }

    private static async Task<JArray?> GetCsvAsJarray(string endpointContent)
    {
        var arr = new JArray();
        await foreach (var record in new CsvStringInputAdapter(endpointContent).GetRecordsAsync())
        {
            var obj = new JObject();
            foreach (var (key, value) in record)
            {
                obj[key] = value?.ToString();
            }
            arr.Add(obj);
        }
        return arr;
    }

    // Legacy compatibility method - delegates to new streaming approach
    private async Task<List<Dictionary<string, string>>> MakeHttpCall(FormUrlEncodedContent? formContent)
    {
        var results = new List<Dictionary<string, string>>();
        
        await foreach (var batch in GetRecordBatchesAsync(1000))
        {
            foreach (var entry in batch)
            {
                var dict = entry.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
                results.Add(dict);
            }
        }
        
        return results;
    }
}