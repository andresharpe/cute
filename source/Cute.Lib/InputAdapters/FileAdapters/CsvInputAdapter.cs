using CsvHelper;
using CsvHelper.Configuration;
using Cute.Lib.OutputAdapters;
using System.Globalization;

namespace Cute.Lib.InputAdapters.FileAdapters;

internal class CsvInputAdapter : InputAdapterBase
{
    private readonly StreamReader _reader;

    private readonly CsvReader _csv;

    public CsvInputAdapter(string contentName, string? fileName, string delimeter = ",")
        : base(fileName ?? contentName + (delimeter == "\t" ? ".tsv" : ".csv"))
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimeter,
        };

        _reader = new(SourceName, System.Text.Encoding.UTF8);

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

    public override Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        if (!_csv.Read())
            return Task.FromResult<IDictionary<string, object?>?>(null);

        var result = new Dictionary<string, object?>();

        var i = 0;

        if (_csv.HeaderRecord is null) return Task.FromResult<IDictionary<string, object?>?>(result);

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
        result.Remove(OutputAdapterBase.StateColumnName);
        return Task.FromResult<IDictionary<string, object?>?>(result);
    }

    public override Task<int> GetRecordCountAsync()
    {
        var lineCounter = 0;
        using StreamReader reader = new(SourceName, System.Text.Encoding.UTF8);
        while (reader.ReadLine() != null)
        {
            lineCounter++;
        }
        return Task.FromResult(lineCounter - 1); // ignore header
    }
}