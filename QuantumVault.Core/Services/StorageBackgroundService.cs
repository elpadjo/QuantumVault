using Microsoft.Extensions.Hosting;
using QuantumVault.Services.Interfaces;

namespace QuantumVault.Core.Services
{
    public class StorageBackgroundService : BackgroundService
    {
        private readonly int _flushInterval = int.Parse(Environment.GetEnvironmentVariable("FLUSH_INTERVAL") ?? "5");
        private readonly int _maxStoreSize = int.Parse(Environment.GetEnvironmentVariable("MAX_STORE_SIZE") ?? "5");
        private readonly int _maxSSTFiles = int.Parse(Environment.GetEnvironmentVariable("MAX_SST_FILES") ?? "10");
        private readonly int _sstCompactionBatchSize = int.Parse(Environment.GetEnvironmentVariable("COMPACTION_BATCH_SIZE") ?? "4");

        private readonly IStoragePersistenceService _persistenceService;
        private readonly IKeyValueStoreService _kvStoreService;
        
        public StorageBackgroundService(IStoragePersistenceService persistenceService, IKeyValueStoreService kvStoreService)
        {
            _persistenceService = persistenceService;
            _kvStoreService = kvStoreService;

            _flushInterval = _flushInterval * 60 * 1000;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                int storeCount = _kvStoreService.GetStoreCount();
                if (storeCount >= _maxStoreSize)                
                    _persistenceService.FlushStoreToSSTable(_maxStoreSize);
                

                int sstCount = _persistenceService.GetSSTableCount();
                if (sstCount > _maxSSTFiles)                
                    _persistenceService.CompactSSTables(_sstCompactionBatchSize, _maxSSTFiles);
                
                await Task.Delay(_flushInterval, stoppingToken);
            }
        }
    }
}
