using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Cute.Lib.Serializers;
using Newtonsoft.Json.Linq;

namespace Cute.Lib.InputAdapters.EntryAdapters;

internal class EntryListInputAdapter(IList<Entry<JObject>> data, ContentType contentType, ContentLocales locales)
    : InputAdapterBase("InMemoryIList")
{
    private readonly IList<Entry<JObject>> _data = data;

    private int _recordNum = 0;

    private readonly EntrySerializer _serializer = new(contentType, locales);

    public new string SourceName = "InMemoryIList";

    public override async Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        if (_recordNum < (await GetRecordCountAsync()))
        {
            var result = _serializer.SerializeEntry(_data[_recordNum++]);

            return result;
        }

        return null;
    }

    public override Task<int> GetRecordCountAsync()
    {
        return Task.FromResult(_data.Count);
    }
}