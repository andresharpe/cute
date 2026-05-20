namespace Cute.Lib.InputAdapters.Http.Models;

public class Pagination
{
    public string SkipKey { get; set; } = default!;
    public string LimitKey { get; set; } = default!;
    public int PageSize { get; set; } = default!;
    public int LimitMax { get; set; } = default!;
}