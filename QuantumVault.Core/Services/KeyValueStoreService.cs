using QuantumVault.Infrastructure.Persistence;
using System.Collections.Concurrent;

namespace QuantumVault.Core.Services
{
    public class KeyValueStoreService : IKeyValueStoreService
    {
        private readonly ConcurrentDictionary<string, string> _store = new();

        public Task PutAsync(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Invalid input: Key and Value are required.");
            }

            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> ReadAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.");
            }

            _store.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task DeleteAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.");
            }

            _store.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task<IDictionary<string, string>> ReadKeyRangeAsync(string startKey, string endKey)
        {
            if (string.IsNullOrWhiteSpace(startKey) || string.IsNullOrWhiteSpace(endKey))
            {
                throw new ArgumentException("StartKey and/or EndKey cannot be empty.");
            }

            var results = _store
                .Where(kv => string.Compare(kv.Key, startKey) >= 0 && string.Compare(kv.Key, endKey) <= 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return Task.FromResult<IDictionary<string, string>>(results);
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
            }
            return Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>(keyValues));
        }
    }
}
