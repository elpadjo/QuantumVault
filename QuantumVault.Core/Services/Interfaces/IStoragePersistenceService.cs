using QuantumVault.Core.Models;
using System.Collections.Concurrent;

namespace QuantumVault.Services.Interfaces
{
    public interface IStoragePersistenceService
    {
        SortedDictionary<string, string> LoadData();
        void SaveData();
        void AppendToLog(string operation, string key, string? value = null);
        void ReplayLog();
        void FlushStoreToSSTable(int maxEntriesPerFile);
        void CompactSSTables(int _sstCompactionBatchSize, int _maxSSTFiles);
        int GetSSTableCount();
        void Enqueue(KeyValueRequestModel request);
        KeyValueRequestModel? Dequeue();
    }
}
