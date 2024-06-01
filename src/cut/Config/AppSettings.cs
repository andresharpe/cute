namespace Cut.Config;

public class AppSettings
{
    public string ApiKey { get; set; } = default!;

    public string DefaultSpace { get; set; } = default!;

    public Dictionary<string, string> Secrets { get; } = [];
}