namespace Cute.Lib.AiModels;

public class AzureOpenAiOptions
{
    public string Endpoint { get; set; } = default!;
    public string ApiKey { get; set; } = default!;
    public string DeploymentName { get; set; } = default!;
}