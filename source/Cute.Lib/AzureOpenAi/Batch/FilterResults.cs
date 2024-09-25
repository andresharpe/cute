namespace Cute.Lib.AzureOpenAi.Batch;

public class FilterResults
{
    public FilterResult Hate { get; set; } = default!;
    public FilterResult ProtectedMaterialCode { get; set; } = default!;
    public FilterResult ProtectedMaterialText { get; set; } = default!;
    public FilterResult SelfHarm { get; set; } = default!;
    public FilterResult Sexual { get; set; } = default!;
    public FilterResult Violence { get; set; } = default!;
    public FilterResult Jailbreak { get; set; } = default!;
}
