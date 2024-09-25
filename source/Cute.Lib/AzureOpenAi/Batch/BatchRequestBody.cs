namespace Cute.Lib.AzureOpenAi.Batch;
using System.Collections.Generic;

public class BatchRequestBody
{
    public string Model { get; set; } = default!;
    public List<BatchRequestMessage> Messages { get; set; } = default!;
}
