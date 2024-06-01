using Newtonsoft.Json;
using System.Data;

namespace Cut.OutputAdapters;

internal class JsonOutputAdapter : OutputAdapterBase, IDataAdapter
{
    private readonly StreamWriter _writer;

    private readonly JsonTextWriter _json;

    private DataTableHelper? _dataTableHelper;

    private int _count = 0;

    public JsonOutputAdapter(string contentName, string? fileName) : base(contentName, fileName ?? contentName + ".json")
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
        _dataTableHelper ??= new DataTableHelper(table);
    }

    public override void AddRow(DataRow row)
    {
        _dataTableHelper ??= new DataTableHelper(row.Table);

        var obj = _dataTableHelper.ToDictionary(row);

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