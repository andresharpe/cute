namespace Cute.Lib.InputAdapters
{
    internal interface IInputAdapter : IDisposable
    {
        string FileName { get; }

        int GetRecordCount();

        IEnumerable<IDictionary<string, object?>> GetRecords(Action<IDictionary<string, object?>, int>? action = null);

        IDictionary<string, object?>? GetRecord();
    }
}