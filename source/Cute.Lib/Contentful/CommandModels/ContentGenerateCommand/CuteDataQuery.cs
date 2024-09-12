namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public class CuteDataQuery
{
    public string Key { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Query { get; set; } = default!;
    public string JsonSelector { get; set; } = default!;
    public string VariablePrefix { get; set; } = default!;
}