using Cute.Lib.AzureOpenAi;

namespace Cute.Lib.AiModels;

public interface IAzureOpenAiOptionsProvider
{
    AzureOpenAiOptions GetAzureOpenAIClientOptions();
}