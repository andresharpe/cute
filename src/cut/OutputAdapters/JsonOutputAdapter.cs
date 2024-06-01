using Cut.Exceptions;
using Newtonsoft.Json;
using System.Data;

namespace Cut.OutputAdapters;

internal class JsonOutputAdapter : OutputAdapterBase, IOutputAdapter
{
    private readonly StreamWriter _writer;

    private readonly JsonTextWriter _json;

    private DynamicDictionaryBuilder? _dataTableHelper;

    private int _count = 0;

    public JsonOutputAdapter(string contentName, string? fileName) : base(fileName ?? contentName + ".json")
    {
        _writer = new(FileName, false, System.Text.Encoding.UTF8);

        _json = new JsonTextWriter(_writer)
        {
            Formatting = Formatting.Indented
        };

        _json.WriteStartObject();
        _json.WritePropertyName(contentName);
        _json.WriteStartArray();
    }

    public override void AddHeadings(DataTable table)
    {
        _dataTableHelper ??= new DynamicDictionaryBuilder(table.Columns.Cast<DataColumn>()
            .Select(c => c.ColumnName.Split('.'))
            .ToList());
    }

    public override void AddRow(DataRow row)
    {
        if (_dataTableHelper == null)
        {
            throw new CliException("'AddHeadings' should be called before 'AddRows'");
        }

        var obj = _dataTableHelper.ToDictionary(row.ItemArray);

        if (_count > 0)
        {
            _json.WriteRaw(",\n");
        }

        // Currently pretty-fied - although we can proably control this too with a param for more compact output
        _json.WriteRaw(JsonConvert.SerializeObject(obj, Formatting.Indented));

        _count++;
    }

    public override void Dispose()
    {
        _writer.Dispose();
    }

    public override void Save()
    {
        _json.WriteEndArray();
        _json.WriteEndObject();
        _json.Flush();
    }
}