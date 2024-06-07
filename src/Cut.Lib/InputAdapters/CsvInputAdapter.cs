using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace Cut.Lib.InputAdapters;

internal class CsvInputAdapter : InputAdapterBase
{
    private StreamReader _reader;

    private CsvReader _csv;

    private readonly List<string> _columns = [];

    public CsvInputAdapter(string contentName, string? fileName, string delimeter = ",")
        : base(fileName ?? contentName + (delimeter == "\t" ? ".tsv" : ".csv"))
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimeter,
        };

        _reader = new(FileName, System.Text.Encoding.UTF8);

        _csv = new CsvReader(_reader, config);

        ReadHeaders();
    }

    private void ReadHeaders()
    {
        _csv.Read();
        _csv.ReadHeader();
    }

    public override void Dispose()
    {
        _csv.Dispose();
        _reader.Dispose();
    }

    public override IDictionary<string, object?>? GetRecord()
    {
        if (!_csv.Read()) return null;

        var result = new Dictionary<string, object?>();

        var i = 0;
        foreach (var key in _csv.HeaderRecord)
        {
            if (string.IsNullOrEmpty(_csv[i]))
            {
                result.Add(key, null);
            }
            else
            {
                result.Add(key, _csv[i]);
            }
            i++;
        }

        return result;
    }

    public override int GetRecordCount()
    {
        var lineCounter = 0;
        using (StreamReader reader = new(FileName))
        {
            while (reader.ReadLine() != null)
            {
                lineCounter++;
            }
            return lineCounter - 1; // ignore header
        }
    }

    public override IEnumerable<IDictionary<string, object?>> GetRecords(Action<IDictionary<string, object?>, int>? action = null)
    {
        var result = new List<IDictionary<string, object?>>();
        var row = 1;

        while (true)
        {
            var obj = GetRecord();

            if (obj == null)
            {
                break;
            }

            if (action is not null)
            {
                action(obj, row++);
            }

            result.Add(obj);
        }
        return result;
    }
}