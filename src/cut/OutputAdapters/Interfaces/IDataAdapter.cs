using System.Data;

namespace Cut.OutputAdapters
{
    internal interface IDataAdapter : IDisposable
    {
        string FileName { get; }

        void AddHeadings(DataTable table);

        void AddRow(DataRow row);

        void Save();
    }
}