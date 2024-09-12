namespace Cute.Lib.InputAdapters
{
    public interface IInputAdapter : IDisposable
    {
        string SourceName { get; }
        Action<FormattableString>? ActionNotifier { get; set; }
        Action<FormattableString>? ErrorNotifier { get; set; }

        Task<int> GetRecordCountAsync();

        IAsyncEnumerable<IDictionary<string, object?>> GetRecordsAsync();

        Task<IDictionary<string, object?>?> GetRecordAsync();
    }
}