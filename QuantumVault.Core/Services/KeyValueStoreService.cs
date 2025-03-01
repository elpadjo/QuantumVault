using QuantumVault.Infrastructure.Persistence;
using System.Collections.Concurrent;
using System.Text.Json;

namespace QuantumVault.Core.Services
{
    public class KeyValueStoreService : IKeyValueStoreService
    {
        private readonly ConcurrentDictionary<string, string> _store;
        private readonly IStoragePersistenceService _persistenceService;
        private readonly string basePath = Environment.GetEnvironmentVariable("DATA_PATH") ?? "./data";

        public KeyValueStoreService(IStoragePersistenceService persistenceService)
        {
            Directory.CreateDirectory(basePath); // Ensure directory exists

            _persistenceService = persistenceService;
            _store = _persistenceService.LoadData();
        }

        public Task PutAsync(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Invalid input: Key and Value are required.");
            }

            _store[key] = value;
            _persistenceService.AppendToLog("PUT", key, value);
            return Task.CompletedTask;
        }

        public Task<string>? ReadAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be empty.");

            if (_store.TryGetValue(key, out var value))            
                return Task.FromResult(value);            

            foreach (var file in Directory.GetFiles(basePath, "sst_*.json").OrderByDescending(f => f))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                if (data != null && data.TryGetValue(key, out value))
                {
                    return Task.FromResult(value); // Return first found value
                }
            }

            return null;
        }

        public async Task DeleteAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.");
            }

            // Step 1: Remove from in-memory store
            _store.TryRemove(key, out _);

            // Step 2: Record deletion in the WAL (Write-Ahead Log)
            _persistenceService.AppendToLog("DELETE", key);

            // Step 3: Remove from SST files
            foreach (var file in Directory.GetFiles(basePath, "sst_*.json"))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(file));
                if (data != null && data.Remove(key)) // Remove if present
                {
                    await File.WriteAllTextAsync(file, JsonSerializer.Serialize(data));
                }
            }
        }


        public async Task<(IDictionary<string, string> Results, int TotalItems)> ReadKeyRangeAsync(
            string startKey, string endKey, int pageSize, int pageNumber)
        {
            if (string.IsNullOrWhiteSpace(startKey) || string.IsNullOrWhiteSpace(endKey))
            {
                throw new ArgumentException("StartKey and EndKey cannot be empty.");
            }

            if (pageSize <= 0 || pageNumber <= 0)
            {
                throw new ArgumentException("PageSize and PageNumber must be greater than zero.");
            }

            var results = new Dictionary<string, string>();

            // Get totalItems
            int totalItems = _store.Count(kv =>
                string.Compare(kv.Key, startKey, StringComparison.Ordinal) >= 0 &&
                string.Compare(kv.Key, endKey, StringComparison.Ordinal) <= 0);

            // Read from in-memory store first
            var inMemoryResults = _store
                .Where(kv => string.Compare(kv.Key, startKey, StringComparison.Ordinal) >= 0 &&
                             string.Compare(kv.Key, endKey, StringComparison.Ordinal) <= 0)
                .OrderBy(kv => kv.Key) // Ensure sorted order
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            foreach (var kv in inMemoryResults)
            {
                results[kv.Key] = kv.Value;
            }

            // If not enough results, continue searching SST files
            if (results.Count < pageSize)
            {
                int remaining = pageSize - results.Count;

                foreach (var file in Directory.GetFiles(basePath, "sst_*.json").OrderByDescending(f => f))
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(file));
                    if (data == null) continue;

                    // Get totalItems
                    totalItems += _store.Count(kv =>
                        string.Compare(kv.Key, startKey, StringComparison.Ordinal) >= 0 &&
                        string.Compare(kv.Key, endKey, StringComparison.Ordinal) <= 0);

                    var fileResults = data
                        .Where(kv => string.Compare(kv.Key, startKey, StringComparison.Ordinal) >= 0 &&
                                     string.Compare(kv.Key, endKey, StringComparison.Ordinal) <= 0)
                        .OrderBy(kv => kv.Key)
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(remaining)
                        .ToDictionary(kv => kv.Key, kv => kv.Value);

                    foreach (var kv in fileResults)
                    {
                        results[kv.Key] = kv.Value;
                    }

                    if (results.Count >= pageSize)
                    {
                        break; // Stop once we have enough records
                    }
                }
            }

            return (results, totalItems);
        }

        public Task<IDictionary<string, string>> BatchPutAsync(Dictionary<string, string> keyValues)
        {
            if (keyValues == null || keyValues.Count == 0)
            {
                throw new ArgumentException("At least one key-value pair is required.");
            }

            foreach (var kv in keyValues)
            {
                _store[kv.Key] = kv.Value;
                _persistenceService.AppendToLog("PUT", kv.Key, kv.Value);
            }
            return Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>(keyValues));
        }

        public int GetStoreCount()
        {
            return _store.Count;
        }
    }
}
