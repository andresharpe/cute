using Cute.Lib.Enums;
using Newtonsoft.Json;

namespace Cute.Lib.OutputAdapters;

internal class JsonOutputAdapter : OutputAdapterBase, IOutputAdapter
{
    private readonly StreamWriter _writer;

    private readonly JsonTextWriter _json;

    private int _count = 0;

    public JsonOutputAdapter(string contentName, string? fileName) : base(fileName ?? contentName + ".json")
    {
        _writer = new(FileSource, false, System.Text.Encoding.UTF8);

        _json = new JsonTextWriter(_writer)
        {
            Formatting = Formatting.Indented
        };

        _json.WriteStartObject();
        _json.WritePropertyName("Items");
        _json.WriteStartArray();
    }

    public override void AddHeadings(IEnumerable<string?> headings)
    {
        // nothing to do here
    }

    public override void AddRow(IDictionary<string, object?> row, EntryState? state)
    {
        if (_count > 0)
        {
            _json.WriteRaw(",\n");
        }
        row.Add(StateColumnName, state?.ToString());
        // Currently pretty-fied - although we can proably control this too with a param for more compact output
        _json.WriteRaw(JsonConvert.SerializeObject(row, Formatting.Indented));

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