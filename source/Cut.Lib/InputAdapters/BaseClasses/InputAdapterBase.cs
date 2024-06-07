namespace Cut.Lib.InputAdapters;

internal abstract class InputAdapterBase : IInputAdapter
{
    private readonly string _fileName;

    public string FileName => new FileInfo(_fileName).FullName;

    public InputAdapterBase(string fileName)
    {
        _fileName ??= fileName;
    }

    public abstract void Dispose();

    public abstract int GetRecordCount();

    public abstract IEnumerable<IDictionary<string, object?>> GetRecords(Action<IDictionary<string, object?>, int>? action = null);

    public abstract IDictionary<string, object?>? GetRecord();
}