﻿using QuantumVault.Infrastructure.Persistence;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace QuantumVault.Core.Services
{
    public class KeyValueStoreService : IKeyValueStoreService
    {
        private readonly ConcurrentDictionary<string, string> _store;
        private readonly IStoragePersistenceService _persistenceService;
        private readonly string basePath = Environment.GetEnvironmentVariable("DATA_PATH") ?? "./data";

        private readonly int _maxBatchSize = int.Parse(Environment.GetEnvironmentVariable("MAX_BATCH_SIZE") ?? "50");
        private readonly int _maxQueueSize = int.Parse(Environment.GetEnvironmentVariable("MAX_QUEUE_SIZE") ?? "1000");
        private readonly int _writeLimit = int.Parse(Environment.GetEnvironmentVariable("WRITE_LIMIT") ?? "500");
        private readonly int _readLimit = int.Parse(Environment.GetEnvironmentVariable("READ_LIMIT") ?? "500");
        private readonly double _cpuHighThreshold = double.Parse(Environment.GetEnvironmentVariable("CPU_HIGH_THRESHOLD") ?? "80"); // 80%
        private readonly double _cpuLowThreshold = double.Parse(Environment.GetEnvironmentVariable("CPU_LOW_THRESHOLD") ?? "50"); // 50%
        private readonly SemaphoreSlim _writeSemaphore;
        private readonly SemaphoreSlim _readSemaphore;
        private readonly ConcurrentQueue<KeyValuePair<string, string>> _writeQueue = new();

        public KeyValueStoreService(IStoragePersistenceService persistenceService)
        {
            Directory.CreateDirectory(basePath); // Ensure directory exists

            _writeSemaphore = new SemaphoreSlim(_writeLimit, _writeLimit);
            _readSemaphore = new SemaphoreSlim(_readLimit, _readLimit);

            _persistenceService = persistenceService;
            _store = _persistenceService.LoadData();
        }

        public async Task<Task> PutAsync(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))            
                throw new ArgumentException("Invalid input: Key and Value are required.");
            
            if (_writeQueue.Count >= _maxQueueSize)
                throw new InvalidOperationException("Write queue is overloaded. Try again later.");

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


        public async Task<IDictionary<string, string>> BatchPutAsync(Dictionary<string, string> keyValues)
        {
            if (keyValues == null || keyValues.Count == 0)
                throw new ArgumentException("At least one key-value pair is required.");

            // Check system load and reject if overloaded
            AdjustThrottling();

            // Limit batch size to prevent large spikes
            if (keyValues.Count > _maxBatchSize)
                throw new InvalidOperationException($"Batch size exceeds the allowed limit of {_maxBatchSize} items.");

            await _writeSemaphore.WaitAsync();
            try
            {
                foreach (var kv in keyValues)
                {
                    // Ensure queue does not exceed limit
                    if (_writeQueue.Count >= _maxQueueSize)
                        throw new InvalidOperationException("Write queue is overloaded. Try again later.");

                    _writeQueue.Enqueue(kv);
                    _store[kv.Key] = kv.Value;
                    _persistenceService.AppendToLog("PUT", kv.Key, kv.Value);
                }

                return new Dictionary<string, string>(keyValues);
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

        private int _overloadCounter = 0;
        private const int _maxOverloadCount = 3; // Threshold before hard throttling

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
    }
}
