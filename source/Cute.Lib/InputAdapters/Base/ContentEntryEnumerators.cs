using Contentful.Core.Models;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace Cute.Lib.InputAdapters.Base;

public class ContentEntryEnumerators : IEnumerable<IAsyncEnumerable<(Entry<JObject>, int)>>
{
    private readonly List<IAsyncEnumerable<(Entry<JObject>, int)>> _values = new(5);

    public int Length => _values.Count;

    public void Add(IAsyncEnumerable<(Entry<JObject>, int)> value)
    {
        _values.Add(value);
    }

    public IEnumerator<IAsyncEnumerable<(Entry<JObject>, int)>> GetEnumerator()
    {
        return ((IEnumerable<IAsyncEnumerable<(Entry<JObject>, int)>>)_values).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _values.GetEnumerator();
    }

    public IAsyncEnumerable<(Entry<JObject>, int)> this[int index]
    {
        get => _values[index];
    }
}