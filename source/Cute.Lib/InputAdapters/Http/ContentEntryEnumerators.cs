using Contentful.Core.Models;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace Cute.Lib.InputAdapters.Http;

internal class ContentEntryEnumerators : IEnumerable<IAsyncEnumerable<(Entry<JObject>, ContentfulCollection<Entry<JObject>>)>>
{
    private readonly List<IAsyncEnumerable<(Entry<JObject>, ContentfulCollection<Entry<JObject>>)>> _values = new(5);

    public int Length => _values.Count;

    public void Add(IAsyncEnumerable<(Entry<JObject>, ContentfulCollection<Entry<JObject>>)> value)
    {
        _values.Add(value);
    }

    public IEnumerator<IAsyncEnumerable<(Entry<JObject>, ContentfulCollection<Entry<JObject>>)>> GetEnumerator()
    {
        return ((IEnumerable<IAsyncEnumerable<(Entry<JObject>, ContentfulCollection<Entry<JObject>>)>>)_values).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _values.GetEnumerator();
    }

    public IAsyncEnumerable<(Entry<JObject>, ContentfulCollection<Entry<JObject>>)> this[int index]
    {
        get => _values[index];
    }
}