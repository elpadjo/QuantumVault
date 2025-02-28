using System.Collections.Concurrent;

namespace QuantumVault.Infrastructure.Persistence
{
    public interface IKeyValueStoreService
    {
        Task PutAsync(string key, string value);
        Task<string>? ReadAsync(string key);
        Task DeleteAsync(string key);
        Task<IDictionary<string, string>> ReadKeyRangeAsync(string startKey, string endKey);
        Task<IDictionary<string, string>> BatchPutAsync(Dictionary<string, string> keyValues);
        int GetStoreCount();
    }
}
