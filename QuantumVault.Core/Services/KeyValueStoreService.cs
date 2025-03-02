using Microsoft.AspNetCore.Http.HttpResults;
using QuantumVault.Core.Enums;
using QuantumVault.Core.Models;
using QuantumVault.Services.Interfaces;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace QuantumVault.Core.Services
{
    public class KeyValueStoreService : IKeyValueStoreService
    {
        private readonly SortedDictionary<string, string> _store;
        private readonly IStoragePersistenceService _persistenceService;
        private readonly string basePath = Environment.GetEnvironmentVariable("DATA_PATH") ?? "./data";

        private readonly int _maxBatchSize = int.Parse(Environment.GetEnvironmentVariable("MAX_BATCH_SIZE") ?? "2000");
        private readonly int _maxQueueSize = int.Parse(Environment.GetEnvironmentVariable("MAX_QUEUE_SIZE") ?? "5000");
        private readonly int _writeLimit = int.Parse(Environment.GetEnvironmentVariable("WRITE_LIMIT") ?? "1000");
        private readonly int _readLimit = int.Parse(Environment.GetEnvironmentVariable("READ_LIMIT") ?? "2000");
        private readonly double _cpuHighThreshold = double.Parse(Environment.GetEnvironmentVariable("CPU_HIGH_THRESHOLD") ?? "85"); // 85%
        private readonly double _cpuLowThreshold = double.Parse(Environment.GetEnvironmentVariable("CPU_LOW_THRESHOLD") ?? "60"); // 60%

        private readonly SemaphoreSlim _writeSemaphore;
        private readonly SemaphoreSlim _readSemaphore;
        private readonly ConcurrentQueue<KeyValuePair<string, string>> _writeQueue = new();

        private readonly CancellationTokenSource _cts = new();

        public KeyValueStoreService(IStoragePersistenceService persistenceService)
        {
            Directory.CreateDirectory(basePath); // Ensure directory exists

            _writeSemaphore = new SemaphoreSlim(_writeLimit, _writeLimit);
            _readSemaphore = new SemaphoreSlim(_readLimit, _readLimit);

            Task.Run(ProcessQueueAsync);
            _persistenceService = persistenceService;
            _store = _persistenceService.LoadData();
        }

        public async Task<Task> PutAsync(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))            
                throw new ArgumentException("Invalid input: Key and Value are required.");
            
            if (_writeQueue.Count >= _maxQueueSize)
                throw new InvalidOperationException("Write queue is overloaded. Try again later.");

            key = key.ToLowerInvariant(); // Normalize only when needed

            // manage load
            AdjustThrottling();

            await _writeSemaphore.WaitAsync();
            try
            {
                _writeQueue.Enqueue(new KeyValuePair<string, string>(key, value));
                
                _store[key] = value;
                _persistenceService.AppendToLog("PUT", key, value);
                return Task.CompletedTask;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        public Task<string?> ReadAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be empty.");

            key = key.ToLowerInvariant(); // Normalize only when needed

            if (TryGetValue(key, out var value))
                return Task.FromResult<string?>(value); // Return first found value;

            return Task.FromResult<string?>(null); // Return null instead of an exception
        }

        public async Task DeleteAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be empty.");

            key = key.ToLowerInvariant(); // Normalize key

            bool existsInMem = false;
            bool existsInSST = false;

            // Step 1: Check if the key exists in memory
            if (_store.ContainsKey(key))
            {
                _store.Remove(key);
                existsInMem = true;
            }

            // Step 2: Check if the key exists in SST files
            foreach (var file in Directory.GetFiles(basePath, "sst_*.json"))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(file));
                if (data != null && data.ContainsKey(key))
                {
                    
                    data.Remove(key);
                    await File.WriteAllTextAsync(file, JsonSerializer.Serialize(data));
                    existsInSST = true;
                }
            }

            if (!existsInMem && !existsInSST)
                throw new ArgumentException($"Key '{key}' does not exist in memory or storage.");            

            // Step 3: Record deletion in the WAL (Write-Ahead Log)
            _persistenceService.AppendToLog("DELETE", key);
        }


        public async Task<(IDictionary<string, string> Results, int TotalItems)> ReadKeyRangeAsync(
            string startKey, string endKey, int pageSize, int pageNumber)
        {
            if (string.IsNullOrWhiteSpace(startKey) || string.IsNullOrWhiteSpace(endKey))
                throw new ArgumentException("StartKey and EndKey cannot be empty.");

            startKey = startKey.ToLowerInvariant(); // Normalize keys
            endKey = endKey.ToLowerInvariant();

            if (!TryGetValue(startKey, out _))
                throw new ArgumentException($"StartKey '{startKey}' does not exist in memory or disk.");

            if (!TryGetValue(endKey, out _))
                throw new ArgumentException($"EndKey '{endKey}' does not exist in memory or disk.");

            if (string.Compare(startKey, endKey, StringComparison.Ordinal) > 0)
                throw new InvalidOperationException("StartKey must be less than or equal to EndKey.");

            if (pageSize <= 0 || pageNumber <= 0)
                throw new ArgumentException("PageSize and PageNumber must be greater than zero.");

            var results = new Dictionary<string, string>();

            // Get total matching items count
            int totalItems = _store.Count(kv =>
                string.Compare(kv.Key, startKey, StringComparison.Ordinal) >= 0 &&
                string.Compare(kv.Key, endKey, StringComparison.Ordinal) <= 0);

            // Read from in-memory store first
            var inMemoryResults = _store
                .Where(kv => string.Compare(kv.Key, startKey, StringComparison.Ordinal) >= 0 &&
                             string.Compare(kv.Key, endKey, StringComparison.Ordinal) <= 0)
                .OrderBy(kv => kv.Key)
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

                    totalItems += data.Count(kv =>
                        string.Compare(kv.Key, startKey, StringComparison.Ordinal) >= 0 &&
                        string.Compare(kv.Key, endKey, StringComparison.Ordinal) <= 0);

                    var fileResults = data
                        .Where(kv => string.Compare(kv.Key, startKey, StringComparison.Ordinal) >= 0 &&
                                     string.Compare(kv.Key, endKey, StringComparison.Ordinal) <= 0)
                        .OrderBy(kv => kv.Key)
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


        public async Task<IDictionary<string, string>> BatchPutAsync(Dictionary<string, string> keyValues)
        {
            if (keyValues == null || keyValues.Count == 0)
                throw new ArgumentException("At least one key-value pair is required.");

            // Check system load and adjust batch size accordingly
            int adjustedBatchSize = GetAdjustedBatchSize();

            if (keyValues.Count > _maxBatchSize)
                throw new InvalidOperationException($"Batch size exceeds the allowed limit of {_maxBatchSize} items.");

            await _writeSemaphore.WaitAsync();
            try
            {
                var processedItems = new Dictionary<string, string>();
                var batchQueue = new Queue<KeyValuePair<string, string>>(keyValues);

                while (batchQueue.Count > 0)
                {
                    int currentBatchSize = Math.Min(adjustedBatchSize, batchQueue.Count);
                    var currentBatch = new List<KeyValuePair<string, string>>();

                    for (int i = 0; i < currentBatchSize; i++)
                    {
                        currentBatch.Add(batchQueue.Dequeue());
                    }

                    foreach (var kv in currentBatch)
                    {
                        string normalizedKey = kv.Key.ToLowerInvariant(); // Normalize only when needed

                        if (_writeQueue.Count >= _maxQueueSize)
                            throw new InvalidOperationException("Write queue is overloaded. Try again later.");

                        _writeQueue.Enqueue(new KeyValuePair<string, string>(normalizedKey, kv.Value));
                        _store[normalizedKey] = kv.Value;
                        _persistenceService.AppendToLog("PUT", normalizedKey, kv.Value);
                        processedItems[normalizedKey] = kv.Value;
                    }


                    // Introduce a short delay to manage system pressure if needed
                    if (IsSystemUnderHighLoad())
                        await Task.Delay(50);
                }

                return processedItems;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }


        public int GetStoreCount()
        {
            return _store.Count;
        }

        public void EnqueueRequest(RequestPriority priority, Func<Task> action)
        {
            var request = new KeyValueRequestModel(priority, action);
            _persistenceService.Enqueue(request);
        }

        private async Task ProcessQueueAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var request = _persistenceService.Dequeue();
                if (request != null)
                {
                    await request.Execute();
                }
                else
                {
                    await Task.Delay(100); // Avoid busy-waiting
                }
            }
        }

        private int _overloadCounter = 0;
        private const int _maxOverloadCount = 3; // Threshold before hard throttling

        // Simulated system load checks (replace with real monitoring logic)
        private bool IsSystemUnderHighLoad() => _writeQueue.Count > (_maxQueueSize * 0.7);
        private bool IsSystemUnderVeryHighLoad() => _writeQueue.Count > (_maxQueueSize * 0.9);

        // Dynamically adjusts batch size based on system load
        private int GetAdjustedBatchSize()
        {
            double cpuUsage = GetCpuUsage();
            bool queueHighLoad = IsSystemUnderHighLoad();
            bool queueVeryHighLoad = IsSystemUnderVeryHighLoad();

            if (cpuUsage > 80 || queueVeryHighLoad)
                return _maxBatchSize / 4; // Further reduce for extreme cases
            if (cpuUsage > 60 || queueHighLoad)
                return _maxBatchSize / 2; // Reduce batch size if under heavy load
            return _maxBatchSize;
        }

        private void AdjustThrottling()
        {
            var cpuUsage = GetCpuUsage(); // Implement this method

            if (cpuUsage > _cpuHighThreshold)
            {
                _overloadCounter++; // Increment only on high CPU usage

                if (_writeSemaphore.CurrentCount > 0 && _writeSemaphore.Wait(0)) // Non-blocking wait
                {
                    _readSemaphore.Wait(0); // Also attempt to reduce read load
                }

                if (_overloadCounter >= _maxOverloadCount)
                {
                    throw new InvalidOperationException("System is overloaded. Write operations are temporarily blocked.");
                }
            }
            else
            {
                _overloadCounter = 0; // Reset counter on normal CPU usage

                if (cpuUsage < _cpuLowThreshold)
                {
                    // Only release if the semaphore isn't already at its limit
                    if (_writeSemaphore.CurrentCount < _writeLimit)
                        _writeSemaphore.Release();

                    if (_readSemaphore.CurrentCount < _readLimit)
                        _readSemaphore.Release();
                }
            }
        }

        private static double GetCpuUsage()
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

            Thread.Sleep(500); // Short delay to measure CPU activity

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;

            return (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;
        }

        private bool TryGetValue(string key, out string? value)
        {
            // Check in-memory store first
            if (_store.TryGetValue(key, out value))
                return true;

            // Search SST files
            foreach (var file in Directory.GetFiles(basePath, "sst_*.json"))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                if (data != null && data.TryGetValue(key, out value))
                    return true;
            }

            value = null;
            return false; // Key does not exist anywhere
        }

    }
}
