namespace Cute.Lib.AzureOpenAi.Batch;

public class Choice
{
    public FilterResults ContentFilterResults { get; set; } = default!;
    public string FinishReason { get; set; } = default!;
    public int Index { get; set; }
    public object Logprobs { get; set; } = default!;
    public Message Message { get; set; } = default!;
}
