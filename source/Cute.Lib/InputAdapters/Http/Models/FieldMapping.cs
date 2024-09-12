namespace Cute.Lib.InputAdapters.Http.Models;

public class FieldMapping
{
    public string FieldName { get; set; } = default!;
    public string Expression { get; set; } = default!;
    public bool Overwrite { get; set; } = true;
}