namespace Cute.Lib.InputAdapters.Http.Models;

public class OpenAiDataAdapterConfig : BaseDataAdapterConfig
{
    public string SystemMessage { get; set; } = default!;
    public string Prompt { get; set; } = default!;
    public string DeploymentModel { get; set; } = default!;
}