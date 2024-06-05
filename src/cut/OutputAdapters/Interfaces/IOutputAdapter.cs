using System.Data;

namespace Cut.OutputAdapters
{
    internal interface IOutputAdapter : IDisposable
    {
        string FileName { get; }

        void AddHeadings(IEnumerable<string> headings);

        void AddRow(IDictionary<string, object?> row);

        void Save();
    }
}