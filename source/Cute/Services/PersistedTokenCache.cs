using Cute.Config;
using Cute.Constants;
using Cute.Lib.Exceptions;
using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;

namespace Cute.Services;

public class PersistedTokenCache(
    IDataProtectionProvider provider) : IPersistedTokenCache
{
    private readonly IDataProtectionProvider _provider = provider;

    private const string _protectorPurpose = $"{Globals.AppName}-settings";

    public Task SaveAsync(string tokenName, AppSettings settings)
    {
        var protector = _provider.CreateProtector(_protectorPurpose);
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{tokenName}");
        var content = JsonConvert.SerializeObject(settings);
        return File.WriteAllTextAsync(path, protector.Protect(content));
    }

    public async Task<AppSettings?> LoadAsync(string tokenName)
    {
        var protector = _provider.CreateProtector(_protectorPurpose);
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

    public void Clear(string tokenName)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{tokenName}");

        if (!File.Exists(path)) return;

        File.Delete(path);
    }

    private static AppSettings? TryLoadFromEnvironment()
    {
        var settings = new AppSettings().GetFromEnvironment();

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