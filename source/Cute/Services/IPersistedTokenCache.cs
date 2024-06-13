using Cute.Config;

namespace Cute.Services
{
    public interface IPersistedTokenCache
    {
        Task<AppSettings?> LoadAsync(string tokenName);

        Task SaveAsync(string tokenName, AppSettings settings);
    }
}