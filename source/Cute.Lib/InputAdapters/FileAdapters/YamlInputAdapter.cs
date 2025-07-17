using Cute.Lib.OutputAdapters;
using YamlDotNet.Serialization;

namespace Cute.Lib.InputAdapters.FileAdapters;

internal class YamlInputAdapter : InputAdapterBase
{
    private readonly JsonInputData? _data;

    private int _recordNum = 0;

    public YamlInputAdapter(string contentName, string? fileName) : base(fileName ?? contentName + ".json")
    {
        var yaml = new DeserializerBuilder()
            .Build();

        _data = yaml.Deserialize<JsonInputData>(File.ReadAllText(SourceName));
    }

    public override async Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        if (_recordNum < await GetRecordCountAsync())
        {
            var record = _data?.Items[_recordNum++];
            record?.Remove(OutputAdapterBase.StateColumnName);
            return record;
        }

        return null;
    }

    public override Task<int> GetRecordCountAsync()
    {
        return Task.FromResult(_data?.Items.Count ?? 0);
    }
}