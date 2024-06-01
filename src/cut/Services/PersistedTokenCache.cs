using Cut.Config;
using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;

namespace Cut.Services;

public class PersistedTokenCache : IPersistedTokenCache
{
    private readonly IDataProtectionProvider _provider;
    private const string ProtectorPurpose = "cut-token";

    public PersistedTokenCache(
        IDataProtectionProvider provider)
    {
        _provider = provider;
    }

    public Task SaveAsync(string tokenName, AppSettings settings)
    {
        var protector = _provider.CreateProtector(ProtectorPurpose);
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{tokenName}");
        var content = JsonConvert.SerializeObject(settings);
        return File.WriteAllTextAsync(path, protector.Protect(content));
    }

    public async Task<AppSettings?> LoadAsync(string tokenName)
    {
        var protector = _provider.CreateProtector(ProtectorPurpose);
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{tokenName}");
        if (!File.Exists(path)) return null;
        var content = await File.ReadAllTextAsync(path);
        var json = protector.Unprotect(content);
        var settings = JsonConvert.DeserializeObject<AppSettings>(json);
        return settings;
    }
}