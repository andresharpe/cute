namespace Cute.Lib.AzureOpenAi.Batch;

public class BatchJobResponseBody
{
    public Choice[] Choices { get; set; } = default!;
    public int Created { get; set; }
    public string Id { get; set; } = default!;
    public string Model { get; set; } = default!;
    public string Object { get; set; } = default!;
    public PromptFilterResults[] PromptFilterResults { get; set; } = default!;
    public string SystemFingerprint { get; set; } = default!;
    public TokenUsage Usage { get; set; } = default!;
}
