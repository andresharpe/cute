namespace Cut.OutputAdapters;

internal class DynamicDictionaryBuilder
{
    private readonly IEnumerable<string[]> _columns;

    public DynamicDictionaryBuilder(IEnumerable<string[]> columns)
    {
        _columns = columns;
    }

    public Dictionary<string, object?> ToDictionary(IReadOnlyList<object?> row, bool ignoreNull = false)
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
                            tmp.Add(fieldNamePath[i][..^2], row[column]?.ToString()?.Split('|') ?? null);
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