using CsvHelper;
using CsvHelper.Configuration;
using System.Data;
using System.Globalization;

namespace Cut.OutputAdapters;

internal class CsvOutputAdapter : OutputAdapterBase, IDataAdapter
{
    private readonly StreamWriter _writer;

    private readonly CsvWriter _csv;

    public CsvOutputAdapter(string contentName, string? fileName, string delimeter = ",")
        : base(contentName, fileName ?? contentName + (delimeter == "\t" ? ".tsv" : ".csv"))
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimeter,
        };

        _writer = new(FileName, false, System.Text.Encoding.UTF8);

        _csv = new CsvWriter(_writer, config);
    }

    public override void AddHeadings(DataTable table)
    {
        foreach (DataColumn col in table.Columns)
        {
            _csv.WriteField(col.ColumnName);
        }
        _csv.NextRecord();
    }

    public override void AddRow(DataRow row)
    {
        foreach (var value in row.ItemArray)
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