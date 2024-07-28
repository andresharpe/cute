using Cute.Config;
using Cute.Constants;
using Cute.Lib.Exceptions;
using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;

namespace Cute.Services;

public class PersistedTokenCache : IPersistedTokenCache
{
    private readonly IDataProtectionProvider _provider;

    private const string ProtectorPurpose = $"{Globals.AppName}-settings";

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
        if (!File.Exists(path)) return TryLoadFromEnvironment();
        var content = await File.ReadAllTextAsync(path);
        try
        {
            var json = protector.Unprotect(content);
            return JsonConvert.DeserializeObject<AppSettings>(json);
        }
        catch
        {
            throw new CliException($"The secure store may be corrupt. ({path})");
        }
    }

    private static AppSettings? TryLoadFromEnvironment()
    {
        var settings = new AppSettings().SetFromEnvironment();

        if (string.IsNullOrEmpty(settings.ContentfulManagementApiKey))
        {
            return null;
        }

        if (string.IsNullOrEmpty(settings.ContentfulDeliveryApiKey))
        {
            return null;
        }

        return settings;
    }
}