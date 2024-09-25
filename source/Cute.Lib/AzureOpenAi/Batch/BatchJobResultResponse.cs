namespace Cute.Lib.AzureOpenAi.Batch;

public class BatchJobResultResponse
{
    public string CustomId { get; set; } = default!;
    public BatchJobResponse Response { get; set; } = default!;
    public object Error { get; set; } = default!;
}
