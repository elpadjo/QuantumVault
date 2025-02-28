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

        public StoragePersistenceService()
        {
            string basePath = Environment.GetEnvironmentVariable("DATA_PATH") ?? "./data";
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
    }
}
