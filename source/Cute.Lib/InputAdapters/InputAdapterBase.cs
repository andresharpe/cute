namespace Cute.Lib.InputAdapters;

public abstract class InputAdapterBase : IInputAdapter
{
    private readonly string _sourceName;

    public string SourceName => _sourceName;

    public InputAdapterBase(string sourceName)
    {
        _sourceName ??= sourceName;
    }

    public virtual async IAsyncEnumerable<IDictionary<string, object?>> GetRecordsAsync()
    {
        while (true)
        {
            var obj = await GetRecordAsync();

            if (obj == null)
            {
                break;
            }

            yield return obj;
        }
    }

    public abstract Task<IDictionary<string, object?>?> GetRecordAsync();

    public abstract Task<int> GetRecordCountAsync();

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public Action<FormattableString>? ActionNotifier { get; set; }
    public Action<FormattableString>? ErrorNotifier { get; set; }
    public Action<int, int, FormattableString>? CountProgressNotifier { get; set; }
}