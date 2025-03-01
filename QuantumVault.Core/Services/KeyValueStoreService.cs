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


        public async Task<IDictionary<string, string>> ReadKeyRangeAsync(string startKey, string endKey)
        {
            if (string.IsNullOrWhiteSpace(startKey) || string.IsNullOrWhiteSpace(endKey))
            {
                throw new ArgumentException("StartKey and/or EndKey cannot be empty.");
            }

            Dictionary<string, string> results = new();

            // Step 1: Check in-memory store first
            foreach (var kv in _store.Where(kv =>
                        string.Compare(kv.Key, startKey, StringComparison.Ordinal) >= 0 &&
                        string.Compare(kv.Key, endKey, StringComparison.Ordinal) <= 0))
            {
                results[kv.Key] = kv.Value;
            }

            // Step 2: Ensure endKey is explicitly included if it exists in _store
            if (_store.TryGetValue(endKey, out var endValue) && !results.ContainsKey(endKey))
            {
                results[endKey] = endValue;
            }

            // Step 3: Check SST files in order of recency
            foreach (var file in Directory.GetFiles(basePath, "sst_*.json").OrderByDescending(f => f))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(file));
                if (data == null) continue;

                foreach (var kv in data)
                {
                    if (string.Compare(kv.Key, startKey, StringComparison.Ordinal) >= 0 &&
                        string.Compare(kv.Key, endKey, StringComparison.Ordinal) <= 0 &&
                        !results.ContainsKey(kv.Key))  // Avoid overwriting newer values
                    {
                        results[kv.Key] = kv.Value;
                    }
                }

                // Step 4: Ensure endKey is explicitly included if found in SST
                if (data.TryGetValue(endKey, out endValue) && !results.ContainsKey(endKey))
                {
                    results[endKey] = endValue;
                }
            }

            return results;
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
