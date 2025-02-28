using QuantumVault.Infrastructure.Persistence;
using System.Collections.Concurrent;
using System.Text.Json;

namespace QuantumVault.Core.Services
{
    public class StoragePersistenceService : IStoragePersistenceService
    {
        private readonly string _filePath;
        private readonly string _logFilePath;
        private readonly object _lock = new();
        private readonly string basePath = Environment.GetEnvironmentVariable("DATA_PATH") ?? "./data";

        public StoragePersistenceService()
        {
            
            Directory.CreateDirectory(basePath); // Ensure directory exists

            _filePath = Path.Combine(basePath, "data_store.json");
            _logFilePath = Path.Combine(basePath, "data_store.log");

            ReplayLog();
        }

        private ConcurrentDictionary<string, string> _store = new();

        public ConcurrentDictionary<string, string> LoadData()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    _store = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(json) ?? new();
                }
                catch
                {
                    _store = new();
                }
            }
            return _store;
        }

        public void SaveData()
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_store, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_filePath, json);
                    File.WriteAllText(_logFilePath, string.Empty); // Clear WAL after persistence
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving data: {ex.Message}");
                }
            }
        }

        public void AppendToLog(string operation, string key, string? value = null)
        {
            lock (_lock)
            {
                using var writer = new StreamWriter(_logFilePath, append: true);
                writer.WriteLine($"{operation}|{key}|{value}");
            }
        }

        public void ReplayLog()
        {
            if (!File.Exists(_logFilePath)) return;

            try
            {
                foreach (var line in File.ReadLines(_logFilePath))
                {
                    var parts = line.Split('|');
                    if (parts.Length < 2) continue;

                    var operation = parts[0];
                    var key = parts[1];
                    var value = parts.Length > 2 ? parts[2] : null;

                    if (operation == "PUT" && value != null)
                    {
                        _store[key] = value;
                    }
                    else if (operation == "DELETE")
                    {
                        _store.TryRemove(key, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error replaying WAL: {ex.Message}");
            }
        }

        public void FlushStoreToSSTable()
        {
            if (_store.Count == 0) return;

            var sstFileName = Path.Combine(basePath, $"sst_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
            var sortedData = _store.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value);

            File.WriteAllText(sstFileName, JsonSerializer.Serialize(sortedData));

            _store.Clear(); // Clear memory after flush
        }

        public void CompactSSTables()
        {
            var sstFiles = Directory.GetFiles(basePath, "sst_*.json").OrderBy(f => f).ToList();

            if (sstFiles.Count < 2) return; // No need to compact if there's only one SSTable

            Dictionary<string, string> mergedData = new();

            foreach (var file in sstFiles)
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                if (data != null)
                {
                    foreach (var kv in data)
                    {
                        mergedData[kv.Key] = kv.Value; // Keep latest value
                    }
                }
                File.Delete(file); // Remove old file after merging
            }

            var newSSTFile = $"sst_{DateTime.UtcNow:yyyyMMddHHmmss}_compacted.json";
            File.WriteAllText(newSSTFile, JsonSerializer.Serialize(mergedData));
        }
    }
}
