namespace Cute.Lib.AzureOpenAi.Batch;

public class TokenUsage
{
    public int CompletionTokens { get; set; }
    public int PromptTokens { get; set; }
    public int TotalTokens { get; set; }
}
