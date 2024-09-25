namespace Cute.Lib.AzureOpenAi.Batch;
using System.Collections.Generic;

public class CreateBatchJobResponse
{
    public int? CancelledAt { get; set; }
    public int? CancellingAt { get; set; }
    public int? CompletedAt { get; set; }
    public string CompletionWindow { get; set; } = default!;
    public int CreatedAt { get; set; }
    public string? ErrorFileId { get; set; }
    public int? ExpiredAt { get; set; }
    public int ExpiresAt { get; set; }
    public int? FailedAt { get; set; }
    public int? FinalizingAt { get; set; }
    public string Id { get; set; } = default!;
    public int? InProgressAt { get; set; }
    public string InputFileId { get; set; } = default!;
    public object? Errors { get; set; }
    public Dictionary<string, object?>? Metadata { get; set; }
    public string @Object { get; set; } = default!;
    public string? OutputFileId { get; set; }
    public RequestCounts RequestCounts { get; set; } = default!;
    public string Status { get; set; } = default!;
}
