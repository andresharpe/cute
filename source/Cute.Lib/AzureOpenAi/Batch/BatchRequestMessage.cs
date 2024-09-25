namespace Cute.Lib.AzureOpenAi.Batch;

public class BatchRequestMessage
{
    public string Role { get; set; } = default!;
    public string Content { get; set; } = default!;
}
