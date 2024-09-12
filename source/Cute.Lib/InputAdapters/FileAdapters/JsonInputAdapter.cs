using Newtonsoft.Json;

namespace Cute.Lib.InputAdapters.FileAdapters;

internal class JsonInputAdapter : InputAdapterBase
{
    private readonly JsonInputData? _data;

    private int _recordNum = 0;

    public JsonInputAdapter(string contentName, string? fileName) : base(fileName ?? contentName + ".json")
    {
        _data = JsonConvert.DeserializeObject<JsonInputData>(File.ReadAllText(SourceName));
    }

    public override async Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        if (_recordNum < await GetRecordCountAsync())
        {
            return _data?.Items[_recordNum++];
        }

        return null;
    }

    public override Task<int> GetRecordCountAsync()
    {
        return Task.FromResult(_data?.Items.Count ?? 0);
    }
}