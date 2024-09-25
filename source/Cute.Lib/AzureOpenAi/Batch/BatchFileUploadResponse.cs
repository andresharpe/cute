namespace Cute.Lib.AzureOpenAi.Batch;

public class BatchFileUploadResponse
{
    public string Status { get; set; } = default!;
    public int Bytes { get; set; }
    public string Purpose { get; set; } = default!;
    public string Filename { get; set; } = default!;
    public string Id { get; set; } = default!;
    public int CreatedAt { get; set; }
    public string Object { get; set; } = default!;
}
