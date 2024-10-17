namespace Cute.Lib.Contentful.CommandModels.ContentSyncApi;

public class CuteContentSyncApi
{
    public string Key { get; set; } = default!;
    public int Order { get; set; } = default!;
    public string Yaml { get; set; } = default!;
    public string Schedule { get; set; } = default!;
    public bool IsRunAfter => Schedule.StartsWith("runafter:", StringComparison.OrdinalIgnoreCase);
    public bool IsTimeScheduled => !IsRunAfter;
}