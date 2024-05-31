using System.Data;
using YamlDotNet.Serialization;

namespace Cut.DataAdapters;

internal class YamlAdapter : DataAdapterBase, IDataAdapter
{

    private Dictionary<string, List<object>> _data = new();

    private readonly List<object> _objects;

    private readonly List<string[]> _columns = new();

    const string initialContent = "---\nversion: 1\n...";

    public YamlAdapter(string contentName, string? fileName) : base(contentName, fileName ?? contentName + ".yaml")
    {
        _objects = [];
        _data.Add(contentName, _objects);
    }

    public override void AddHeadings(DataTable table)
    {
        foreach (DataColumn col in table.Columns)
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
                        if (row[column] == DBNull.Value) // may surpress this later, better to see the key and null
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

        _objects.Add(obj);

    }

    public override void Dispose()
    {
    }

    public override void Save()
    {
        var serializer = new SerializerBuilder().Build();
        File.WriteAllText(FileName, serializer.Serialize(_data));
    }
}
