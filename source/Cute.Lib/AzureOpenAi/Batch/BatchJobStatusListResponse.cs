namespace Cute.Lib.AzureOpenAi.Batch;
using System.Collections.Generic;

public class BatchJobStatusListResponse
{
    public List<CreateBatchJobResponse> Data { get; set; } = default!;
}
