namespace Cute.Lib.InputAdapters.FileAdapters;

internal class JsonInputData
{
    public List<Dictionary<string, object?>> Items { get; set; } = default!;
}