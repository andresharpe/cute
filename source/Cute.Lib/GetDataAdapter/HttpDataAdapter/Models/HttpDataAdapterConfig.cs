namespace Cute.Lib.GetDataAdapter;

public class HttpDataAdapterConfig
{
    public string Id { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string ContentDisplayField { get; set; } = default!;
    public string ContentKeyField { get; set; } = default!;
    public string EndPoint { get; set; } = default!;
    public string ContinuationTokenHeader { get; set; } = default!;
    public HttpMethod HttpMethod { get; set; } = HttpMethod.Get;
    public Dictionary<string, string> Headers { get; set; } = default!;
    public Dictionary<string, string> FormUrlEncodedContent { get; set; } = default!;
    public ResultsFormat ResultsFormat { get; set; } = ResultsFormat.Json;
    public string ResultsSecret { get; set; } = default!;
    public string ResultsJsonPath { get; set; } = default!;
    public FieldMapping[] Mapping = [];
    public VarMapping[] PreMapping = [];
    public List<ContentEntryDefinition> EnumerateForContentTypes = [];
    public string FilterExpression = default!;
    public Pagination Pagination { get; set; } = default!;
}