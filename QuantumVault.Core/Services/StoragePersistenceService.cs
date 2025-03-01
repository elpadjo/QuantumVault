using QuantumVault.Infrastructure.Persistence;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QuantumVault.Core.Services
{
    public class StoragePersistenceService : IStoragePersistenceService
    {
        private readonly string _filePath;
        private readonly string _logFilePath;
        private readonly object _lock = new();
        private readonly string basePath = Environment.GetEnvironmentVariable("DATA_PATH") ?? "./data";

        private readonly int _maxEntries = 1000;
        private readonly LinkedList<KeyValuePair<string, string>> _recentEntries;

        public StoragePersistenceService()
        {
            
            Directory.CreateDirectory(basePath); // Ensure directory exists

            _filePath = Path.Combine(basePath, "data_store.json");
            _logFilePath = Path.Combine(basePath, "data_store.log");
            _recentEntries = new LinkedList<KeyValuePair<string, string>>();

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
                    //load old data
                    LoadExistingSnapshot();

                    AddEntries(_store);

                    using var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

                    writer.WriteStartObject(); // Start JSON object
                    foreach (var entry in _recentEntries)
                    {
                        writer.WritePropertyName(entry.Key?.ToString()); // Ensure key is a string
                        JsonSerializer.Serialize(writer, entry.Value); // Serialize value properly
                    }
                    writer.WriteEndObject(); // End JSON object

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

                string logEntry = $"{operation}|{key}|{value ?? string.Empty}";
                string checksum = ComputeSHA256(logEntry);
                writer.WriteLine($"{logEntry}|{checksum}");
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
                    if (parts.Length < 3) continue; // Ensure there are enough parts

                    string operation = parts[0];
                    string key = parts[1];
                    string value = parts.Length > 3 ? parts[2] : null;
                    string storedChecksum = parts[^1]; // Last part is the checksum

                    // Recompute checksum and validate
                    string logEntryWithoutChecksum = $"{operation}|{key}|{value ?? string.Empty}";
                    string computedChecksum = ComputeSHA256(logEntryWithoutChecksum);

                    if (!string.Equals(storedChecksum, computedChecksum, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Corruption detected in log entry: {line}");
                        continue; // Skip corrupted entry
                    }

                    // Apply valid log entry
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

        public void CompactSSTables(int _sstCompactionBatchSize)
        {
            var sstFiles = Directory.GetFiles(basePath, "sst_*.json").OrderBy(f => f).ToList();

            if (sstFiles.Count <= _sstCompactionBatchSize) return; // Ensure enough files to compact

            // Select the oldest `_sstCompactionBatchSize` files for compaction
            var filesToCompact = sstFiles.Take(_sstCompactionBatchSize).ToList();

            Dictionary<string, string> mergedData = new();

            foreach (var file in filesToCompact)
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

            // Extract the earliest timestamp from the batch (i.e., the last file in `filesToCompact`)
            string earliestTimestamp = Path.GetFileName(filesToCompact.Last()).Split('_')[1];

            // Name the new compacted SST file using the earliest timestamp
            var newSSTFile = Path.Combine(basePath, $"sst_{earliestTimestamp}_compacted.json");
            File.WriteAllText(newSSTFile, JsonSerializer.Serialize(mergedData));
        }

        public int GetSSTableCount()
        {
            var sstFiles = Directory.GetFiles(basePath, "sst_*.json").ToList();
            return sstFiles.Count;
        }

        private void LoadExistingSnapshot()
        {
            if (!File.Exists(_filePath)) return;

            lock (_lock)
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dictionary != null)
                    {
                        foreach (var entry in dictionary)
                        {
                            _recentEntries.AddLast(new KeyValuePair<string, string>(entry.Key, entry.Value));
                            if (_recentEntries.Count > _maxEntries)
                                _recentEntries.RemoveFirst();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading snapshot: {ex.Message}");
                }
            }
        }

        private void AddEntries(ConcurrentDictionary<string, string> entries)
        {
            lock (_lock)
            {
                foreach (var entry in entries)
                {
                    _recentEntries.AddLast(new KeyValuePair<string, string>(entry.Key, entry.Value));

                    if (_recentEntries.Count > _maxEntries)
                        _recentEntries.RemoveFirst();
                }
            }
        }

        private string ComputeSHA256(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash); // Returns the hash as a hex string
        }


    }
}
