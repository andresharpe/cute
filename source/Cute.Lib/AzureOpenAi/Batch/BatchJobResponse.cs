namespace Cute.Lib.AzureOpenAi.Batch;

public class BatchJobResponse
{
    public BatchJobResponseBody Body { get; set; } = default!;
    public string RequestId { get; set; } = default!;
    public int StatusCode { get; set; }
}
