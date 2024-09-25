namespace Cute.Lib.AzureOpenAi.Batch;

public class PromptFilterResults
{
    public int PromptIndex { get; set; }
    public FilterResults ContentFilterResults { get; set; } = default!;
}