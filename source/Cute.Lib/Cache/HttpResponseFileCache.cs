using Newtonsoft.Json;

namespace Cute.Lib.Cache;

public class HttpResponseFileCache
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), "cute");

    public async Task<T?> Get<T>(string fileBaseName, Func<Task<T>> createObject)
    {
        var filename = Path.Combine(_tempPath, $"{fileBaseName}.json");

        Directory.CreateDirectory(_tempPath);

        if (File.Exists(filename))
        {
            return JsonConvert.DeserializeObject<T>(await File.ReadAllTextAsync(filename));
        }

        var newObject = await createObject();

        await File.WriteAllTextAsync(filename, JsonConvert.SerializeObject(newObject));

        return newObject;
    }
}

public class HttpResponseCacheEntry
{
    public string RequestUri { get; set; } = default!;

    public object? ResponseContent { get; set; } = default!;

    public Dictionary<string, string?> ResponseContentHeaders { get; set; } = [];
}