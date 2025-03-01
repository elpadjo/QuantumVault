using QuantumVault.Core.Enums;

namespace QuantumVault.Services.Interfaces
{
    public interface IKeyValueStoreService
    {
        Task<Task> PutAsync(string key, string value);
        Task<string?> ReadAsync(string key);
        Task DeleteAsync(string key);
        Task<(IDictionary<string, string> Results, int TotalItems)> ReadKeyRangeAsync(
            string startKey, string endKey, int pageSize, int pageNumber);
        Task<IDictionary<string, string>> BatchPutAsync(Dictionary<string, string> keyValues);
        int GetStoreCount();
        void EnqueueRequest(RequestPriority priority, Func<Task> action);
    }
}
