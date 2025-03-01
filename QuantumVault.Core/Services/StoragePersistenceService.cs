﻿using QuantumVault.Core.Enums;
using QuantumVault.Core.Models;
using QuantumVault.Services.Interfaces;
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
        private readonly string _journalFilePath;
        private readonly object _lock = new();
        private readonly string basePath = Environment.GetEnvironmentVariable("DATA_PATH") ?? "./data";

        private readonly int _maxEntries = 1000;
        private readonly LinkedList<KeyValuePair<string, string>> _recentEntries;
        public readonly SortedDictionary<int, Queue<KeyValueRequestModel>> _taskQueue = new();

        public StoragePersistenceService()
        {
            
            Directory.CreateDirectory(basePath); // Ensure directory exists

            _filePath = Path.Combine(basePath, "data_store.json");
            _logFilePath = Path.Combine(basePath, "data_store.log");
            _journalFilePath = Path.Combine(basePath, "data_store.journal");

            _recentEntries = new LinkedList<KeyValuePair<string, string>>();

            RecoverFromJournal();
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
                    string tempFile = Path.Combine(basePath, "data_store.tmp");
                    WriteToJournal("SNAPSHOT", tempFile);

                    // Load old data & merge
                    LoadExistingSnapshot();
                    AddEntries(_store);

                    using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();
                        foreach (var entry in _recentEntries)
                        {
                            writer.WritePropertyName(entry.Key);
                            JsonSerializer.Serialize(writer, entry.Value);
                        }
                        writer.WriteEndObject();
                    } // <-- Stream is now properly closed before replacement

                    // Atomically replace old snapshot
                    if (File.Exists(_filePath))
                    {
                        File.Replace(tempFile, _filePath, null);
                    }
                    else
                    {
                        File.Move(tempFile, _filePath);
                    }

                    MarkJournalCommitted(); // Mark success

                    File.WriteAllText(_logFilePath, string.Empty); // Clear WAL after persistence
                }
                catch (IOException ex) when (ex.Message.Contains("used by another process"))
                {
                    Console.WriteLine("File is locked, retrying in 100ms...");
                    Thread.Sleep(100);
                    SaveData(); // Retry logic
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

            string sstFileName = Path.Combine(basePath, $"sst_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
            WriteToJournal("SSTABLE", sstFileName);

            var sortedData = _store.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value);

            File.WriteAllText(sstFileName, JsonSerializer.Serialize(sortedData));

            _store.Clear(); // Clear memory after flush

            MarkJournalCommitted();
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

        public void Enqueue(KeyValueRequestModel request)
        {
            lock (_lock)
            {
                if (!_taskQueue.ContainsKey((int)request.Priority))
                    _taskQueue[(int)request.Priority] = new Queue<KeyValueRequestModel>();

                _taskQueue[(int)request.Priority].Enqueue(request);
            }
        }

        public KeyValueRequestModel? Dequeue()
        {
            lock (_lock)
            {
                foreach (var key in _taskQueue.Keys.OrderBy(k => k))
                {
                    if (_taskQueue[key].Count > 0)
                        return _taskQueue[key].Dequeue();
                }
            }
            return null;
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

        private void WriteToJournal(string operation, string targetFile)
        {
            lock (_lock)
            {
                var journalEntry = $"{DateTime.UtcNow:O}|{operation}|{targetFile}|IN_PROGRESS";
                File.WriteAllText(_journalFilePath, journalEntry); // Overwrite journal with latest transaction
            }
        }

        private void MarkJournalCommitted()
        {
            lock (_lock)
            {
                if (File.Exists(_journalFilePath))
                {
                    var lines = File.ReadAllText(_journalFilePath);
                    if (!string.IsNullOrEmpty(lines))
                    {
                        File.WriteAllText(_journalFilePath, lines.Replace("IN_PROGRESS", "COMMITTED"));
                    }
                }
            }
        }

        private void RecoverFromJournal()
        {
            if (!File.Exists(_journalFilePath)) return;

            try
            {
                var entry = File.ReadAllText(_journalFilePath).Split('|');
                if (entry.Length < 4 || entry[3] == "COMMITTED") return; // Skip if already committed

                var operation = entry[1];
                var targetFile = entry[2];

                if (operation == "SNAPSHOT" && File.Exists(targetFile))
                {
                    // Recover last snapshot if necessary
                    File.Replace(targetFile, _filePath, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recovering from journal: {ex.Message}");
            }
        }


    }
}
