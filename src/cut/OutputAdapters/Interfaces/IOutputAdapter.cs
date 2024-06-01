using System.Data;

namespace Cut.OutputAdapters
{
    internal interface IOutputAdapter : IDisposable
    {
        string FileName { get; }

        void AddHeadings(DataTable table);

        void AddRow(DataRow row);

        void Save();
    }
}