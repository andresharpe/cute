using Cut.Exceptions;
using System.Data;

namespace Cut.DataAdapters;

internal abstract class DataAdapterBase : IDataAdapter
{

    private readonly string _fileName;

    public string FileName => new FileInfo(_fileName).FullName;

    public DataAdapterBase(string contentName, string fileName)
    {
        _fileName ??= fileName;
        try
        {
            File.Delete(_fileName);
        }
        catch (IOException ex)
        {
            throw new CliException(ex.Message, ex);
        }
    }

    public abstract void Dispose();

    public abstract void AddHeadings(DataTable table);

    public abstract void AddRow(DataRow row);

    public abstract void Save();

}
