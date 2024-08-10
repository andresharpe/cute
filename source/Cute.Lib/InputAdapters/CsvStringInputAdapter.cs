using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace Cute.Lib.InputAdapters;

internal class CsvStringInputAdapter : InputAdapterBase
{
    private StringReader _reader;

    private CsvReader _csv;

    private readonly List<string> _columns = [];

    public CsvStringInputAdapter(string content, string delimeter = ",")
        : base("__memory.csv")
    {
        // 1124001997,CA,Wellington North,,43.9,-80.57,CA-ON,province,,22.6,11914,11914,America/Toronto // 4368009
        // 1124001704,CA,Hanover,,49.4433,-96.8492,CA-MB,province,,21.2,15733,15733,America/Winnipeg

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimeter,
        };

        _reader = new(content);

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

        if (_csv.HeaderRecord is null) return result;

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
        using (StreamReader reader = new(FileName, System.Text.Encoding.UTF8))
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