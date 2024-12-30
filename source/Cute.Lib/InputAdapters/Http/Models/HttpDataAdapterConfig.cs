namespace Cute.Lib.InputAdapters.Http.Models;

public class HttpDataAdapterConfig : BaseDataAdapterConfig
{
    public string EndPoint { get; set; } = default!;
    public string ContinuationTokenHeader { get; set; } = default!;
    public HttpMethod HttpMethod { get; set; } = HttpMethod.Get;
    public Dictionary<string, string> Headers { get; set; } = default!;
    public Dictionary<string, string> FormUrlEncodedContent { get; set; } = default!;
    public List<ContentEntryDefinition> EnumerateForContentTypes { get; set; } = [];
    public string FilterExpression { get; set; } = default!;
    public Pagination Pagination { get; set; } = default!;
}