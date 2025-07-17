using Cute.Lib.Enums;

namespace Cute.Lib.OutputAdapters;

public interface IOutputAdapter : IDisposable
{
    string FileSource { get; }

    void AddHeadings(IEnumerable<string> headings);

    void AddRow(IDictionary<string, object?> row, EntryState? state);

    void Save();
}