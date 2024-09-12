using Cute.Config;

namespace Cute.Services
{
    public interface IPersistedTokenCache
    {
        void Clear(string tokenName);

        Task<AppSettings?> LoadAsync(string tokenName);

        Task SaveAsync(string tokenName, AppSettings settings);
    }
}