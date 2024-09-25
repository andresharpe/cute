namespace Cute.Lib.AzureOpenAi.Batch;

public class FilterResult
{
    public bool Filtered { get; set; } = default!;
    public bool Detected { get; set; } = default!;
    public string Severity { get; set; } = default!;
}
