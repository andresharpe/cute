using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Cute.Lib.Serializers;
using Newtonsoft.Json.Linq;

namespace Cute.Lib.InputAdapters.EntryAdapters;

internal class FlatEntryListInputAdapter(List<IDictionary<string, object?>> data, string? sourceName = null)
    : InputAdapterBase("InMemory")
{
    private readonly List<IDictionary<string, object?>> _data = data;

    private readonly string? _sourceName = sourceName;

    private int _recordNum = 0;

    public new string SourceName => _sourceName ?? "InMemoryIList";

    public override async Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        if (_recordNum < (await GetRecordCountAsync()))
        {
            var result = _data[_recordNum++];

            return result;
        }

        return null;
    }

    public override Task<int> GetRecordCountAsync()
    {
        return Task.FromResult(_data.Count);
    }
}