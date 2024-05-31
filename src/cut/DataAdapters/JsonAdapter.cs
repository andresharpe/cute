using Newtonsoft.Json;
using System.Data;

namespace Cut.DataAdapters;

internal class JsonAdapter : DataAdapterBase, IDataAdapter
{

    private readonly StreamWriter _writer;

    private readonly JsonTextWriter _json;

    private readonly List<string[]> _columns = new ();

    private int _count = 0;

    public JsonAdapter(string contentName, string? fileName) : base(contentName, fileName ?? contentName + ".json")
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
        foreach(DataColumn col in table.Columns) 
        {
            var fieldNamePath = col.ColumnName.Split(".");
            _columns.Add(fieldNamePath);
        }
    }


    public override void AddRow(DataRow row)
    {
        var obj = new Dictionary<string, object?>();
        var column = 0;

        foreach (var fieldNamePath in _columns)
        {
            var tmp = obj;

            for (var i = 0; i < fieldNamePath.Length; i++)
            {
                if (tmp.ContainsKey(fieldNamePath[i]))
                {
                    tmp = (Dictionary<string, object?>)tmp[fieldNamePath[i]]!;
                }
                else
                {
                    if (i == fieldNamePath.Length - 1)
                    {
                        if (row[column] == DBNull.Value) // my surpress this later, better to see the key and null
                        {
                            if (fieldNamePath[i].EndsWith("[]"))
                            {
                                tmp.Add(fieldNamePath[i][..^2], null);
                            }
                            else
                            {
                                tmp.Add(fieldNamePath[i], null);
                            }
                        }
                        else if (fieldNamePath[i].EndsWith("[]"))
                        {
                            tmp.Add(fieldNamePath[i][..^2], ((string)row[column]).Split('|'));
                        }
                        else
                        {
                            tmp.Add(fieldNamePath[i], row[column]);
                        }
                        column++;
                    }
                    else
                    {
                        var newValue = new Dictionary<string, object?>();
                        tmp.Add(fieldNamePath[i], newValue);
                        tmp = newValue;
                    }
                }
            }
        }

        if (_count > 0)
        {
            _json.WriteRaw(",\n");
        }

        // Indenting - although we can proably control this too with a param for more compact output
        _json.WriteRaw(JsonConvert.SerializeObject(obj,Formatting.Indented));

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
