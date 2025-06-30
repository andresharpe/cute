using Cute.Lib.InputAdapters.Base.Models;

namespace Cute.Lib.InputAdapters.Http.Models;

public class HttpDataAdapterConfig : DataAdapterConfigBase
{
    public string EndPoint { get; set; } = default!;
    public string ContinuationTokenHeader { get; set; } = default!;
    public HttpMethod HttpMethod { get; set; } = HttpMethod.Get;
    public Dictionary<string, string> Headers { get; set; } = default!;
    public Dictionary<string, string> FormUrlEncodedContent { get; set; } = default!;
    public ResultsFormat ResultsFormat { get; set; } = ResultsFormat.Json;
    public string ResultsSecret { get; set; } = default!;
    public string ResultsJsonPath { get; set; } = default!;
}