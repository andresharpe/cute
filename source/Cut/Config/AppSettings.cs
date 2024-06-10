namespace Cut.Config;

public class AppSettings
{
    public string ApiKey { get; set; } = default!;
    public string DefaultSpace { get; set; } = default!;
    public string ContentfulManagementApiKey { get; set; } = default!;
    public string OpenAiEndpoint { get; set; } = default!;
    public string OpenAiApiKey { get; set; } = default!;
    public string OpenAiDeploymentName { get; set; } = default!;
    public Dictionary<string, string> Secrets { get; } = [];
}