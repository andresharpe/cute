using Cute.Lib.Exceptions;

namespace Cute.Lib.OutputAdapters;

internal abstract class OutputAdapterBase : IOutputAdapter
{
    private readonly string _fileName;

    public string FileSource => new FileInfo(_fileName).FullName;

    public OutputAdapterBase(string fileName)
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

    public abstract void AddHeadings(IEnumerable<string> headings);

    public abstract void AddRow(IDictionary<string, object?> row);

    public abstract void Save();
}