namespace Cute.Lib.GetDataAdapter;

public class Pagination
{
    public string SkipKey { get; set; } = default!;
    public string LimitKey { get; set; } = default!;
    public int LimitMax { get; set; } = default!;
}