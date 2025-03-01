using System.Collections.Concurrent;

namespace QuantumVault.Services.Interfaces
{
    public interface IStoragePersistenceService
    {
        ConcurrentDictionary<string, string> LoadData();
        void SaveData();
        void AppendToLog(string operation, string key, string? value = null);
        void ReplayLog();
        void FlushStoreToSSTable();
        void CompactSSTables(int _sstCompactionBatchSize);
        int GetSSTableCount();
    }
}
