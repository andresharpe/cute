namespace Cute.Lib.AiModels;

public interface IAzureOpenAiOptionsProvider
{
    AzureOpenAiOptions GetAzureOpenAIClientOptions();
}