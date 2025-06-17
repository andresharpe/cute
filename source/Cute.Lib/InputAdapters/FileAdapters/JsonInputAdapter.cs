using Newtonsoft.Json;

namespace Cute.Lib.InputAdapters.FileAdapters;

internal class JsonInputAdapter : InputAdapterBase
{
    // private readonly JsonInputData? _data;

    private readonly JsonSerializer _serializer = new JsonSerializer
    {
        DateParseHandling = DateParseHandling.DateTimeOffset,
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        NullValueHandling = NullValueHandling.Ignore
    };

    private readonly FileStream _fileStream;
    private readonly StreamReader _streamReader;
    private readonly JsonTextReader _jsonTextReader;

    private int _recordNum = 0;
    private int _recordCount = 0;

    public JsonInputAdapter(string contentName, string? fileName) : base(fileName ?? contentName + ".json")
    {
        using (var fileStream = File.OpenRead(SourceName))
        {
            using (var streamReader = new StreamReader(fileStream))
            {
                var jsonTextReader = new JsonTextReader(streamReader);
                if (
                    jsonTextReader.Read() && jsonTextReader.TokenType == JsonToken.StartObject
                    && jsonTextReader.Read() && jsonTextReader.TokenType == JsonToken.PropertyName
                    && jsonTextReader.Value?.ToString() == "Items"
                    && jsonTextReader.Read() && jsonTextReader.TokenType == JsonToken.StartArray
                )
                {
                    while (jsonTextReader.Read())
                    {
                        if (jsonTextReader.TokenType == JsonToken.StartObject)
                        {
                            _recordCount++;
                            jsonTextReader.Skip();
                        }
                    }
                }
            }
        }

        _fileStream = File.OpenRead(SourceName);
        _streamReader = new StreamReader(_fileStream);
        _jsonTextReader = new JsonTextReader(_streamReader);

        if (
            _jsonTextReader.Read() && _jsonTextReader.TokenType == JsonToken.StartObject
            && _jsonTextReader.Read() && _jsonTextReader.TokenType == JsonToken.PropertyName
            && _jsonTextReader.Value?.ToString() == "Items"
            && _jsonTextReader.Read() && _jsonTextReader.TokenType == JsonToken.StartArray
        )
        {
            return;
        }

        throw new InvalidDataException($"Invalid JSON format in file '{SourceName}'. Expected an object with an 'Items' array.");
    }

    public override async Task<IDictionary<string, object?>?> GetRecordAsync()
    {
        if (_recordNum >= await GetRecordCountAsync())
        {
            return null;
        }

        if (_jsonTextReader.Read())
        {
            if (_jsonTextReader.TokenType == JsonToken.StartObject)
            {
                _recordNum++;
                var result = _serializer.Deserialize<Dictionary<string, object?>>(_jsonTextReader);
                return result;
            }
        }

        return null;
    }

    public override Task<int> GetRecordCountAsync()
    {
        return Task.FromResult(_recordCount);
    }

    public override void Dispose()
    {
        base.Dispose();
        _streamReader?.Dispose();
        _fileStream?.Dispose();
    }
}