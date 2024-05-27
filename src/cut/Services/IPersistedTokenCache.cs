
namespace Cut.Services
{
    public interface IPersistedTokenCache
    {
        Task<string?> LoadAsync(string tokenName);
        Task SaveAsync(string tokenName, string token);
    }
}