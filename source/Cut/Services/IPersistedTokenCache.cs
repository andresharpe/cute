using Cut.Config;

namespace Cut.Services
{
    public interface IPersistedTokenCache
    {
        Task<AppSettings?> LoadAsync(string tokenName);

        Task SaveAsync(string tokenName, AppSettings settings);
    }
}