namespace Cute.Lib.AzureOpenAi.Batch;

public class AzureOpenAiBatchRequest
{
    public string CustomId { get; set; } = default!;
    public string Method { get; set; } = default!;
    public string Url { get; set; } = default!;
    public BatchRequestBody Body { get; set; } = default!;
}
