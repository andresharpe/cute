using CsvHelper;
using CsvHelper.Configuration;
using System.Data;
using System.Globalization;

namespace Cut.Lib.OutputAdapters;

internal class CsvOutputAdapter : OutputAdapterBase, IOutputAdapter
{
    private readonly StreamWriter _writer;

    private readonly CsvWriter _csv;

    public CsvOutputAdapter(string contentName, string? fileName, string delimeter = ",")
        : base(fileName ?? contentName + (delimeter == "\t" ? ".tsv" : ".csv"))
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimeter,
        };

        _writer = new(FileName, false, System.Text.Encoding.UTF8);

        _csv = new CsvWriter(_writer, config);
    }

    public override void AddHeadings(IEnumerable<string> headings)
    {
        foreach (var col in headings)
        {
            _csv.WriteField(col);
        }
        _csv.NextRecord();
    }

    public override void AddRow(IDictionary<string, object?> row)
    {
        foreach (var (_, value) in row)
        {
            _csv.WriteField(value);
        }
        _csv.NextRecord();
    }

    public override void Dispose()
    {
        _csv.Dispose();
        _writer.Dispose();
    }

    public override void Save()
    {
        _csv.Flush();
    }
}