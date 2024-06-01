using System.Data;

namespace Cut.OutputAdapters;

internal class DataTableHelper
{
    private readonly List<string[]> _columns;

    public DataTableHelper(DataTable table)
    {
        _columns = table.Columns.Cast<DataColumn>()
            .Select(c => c.ColumnName.Split('.'))
            .ToList();
    }

    public Dictionary<string, object?> ToDictionary(DataRow row, bool ignoreNull = false)
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
                        if (!ignoreNull && row[column] == DBNull.Value)
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
        return obj;
    }
}