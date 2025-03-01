using System.Collections.Concurrent;

namespace QuantumVault.Infrastructure.Persistence
{
    public interface IKeyValueStoreService
    {
        Task<Task> PutAsync(string key, string value);
        Task<string>? ReadAsync(string key);
        Task DeleteAsync(string key);
        Task<(IDictionary<string, string> Results, int TotalItems)> ReadKeyRangeAsync(
            string startKey, string endKey, int pageSize, int pageNumber);
        Task<IDictionary<string, string>> BatchPutAsync(Dictionary<string, string> keyValues);
        int GetStoreCount();
    }
}
