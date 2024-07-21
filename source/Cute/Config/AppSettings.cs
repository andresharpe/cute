using Cute.Constants;

namespace Cute.Config;

public class AppSettings
{
    public string ContentfulDefaultSpace { get; set; } = default!;
    public string ContentfulDefaultEnvironment { get; set; } = default!;
    public string ContentfulManagementApiKey { get; set; } = default!;
    public string ContentfulDeliveryApiKey { get; set; } = default!;
    public string ContentfulPreviewApiKey { get; set; } = default!;
    public string OpenAiEndpoint { get; set; } = default!;
    public string OpenAiApiKey { get; set; } = default!;
    public string OpenAiDeploymentName { get; set; } = default!;
    public string TempFolder { get; set; } = Path.Combine(Path.GetTempPath(), Globals.AppName);
    public Dictionary<string, string> Secrets { get; } = [];
}