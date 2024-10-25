namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public class CuteDataQuery
{
    public string Key { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Query { get; set; } = default!;
    public string JsonSelector { get; set; } = default!;
    public string VariablePrefix { get; set; } = default!;
}
public class CuteDataQueryLocalized
{
    public Dictionary<string, string> Key { get; set; } = default!;
    public Dictionary<string, string> Title { get; set; } = default!;
    public Dictionary<string, string> Query { get; set; } = default!;
    public Dictionary<string, string> JsonSelector { get; set; } = default!;
    public Dictionary<string, string> VariablePrefix { get; set; } = default!;
    public CuteDataQuery GetBasicEntry(string locale)
    {
        return new CuteDataQuery
        {
            Key = Key[locale],
            Title = Title[locale],
            Query = Query[locale],
            JsonSelector = JsonSelector[locale],
            VariablePrefix = VariablePrefix[locale]
        };
    }
}