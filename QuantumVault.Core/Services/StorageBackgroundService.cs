using Microsoft.Extensions.Hosting;
using QuantumVault.Infrastructure.Persistence;

namespace QuantumVault.Core.Services
{
    public class StorageBackgroundService : BackgroundService
    {
        private readonly int _flushInterval = 1000 * 60 * 1000; // 1000 minutes
        private readonly int _maxStoreSize = 10; // Flush when size reaches 10
        private readonly int _maxSSTFiles = 10; // Compact when total sst files reaches 10
        private readonly int _sstCompactionBatchSize = 3;

        private readonly IStoragePersistenceService _persistenceService;
        private readonly IKeyValueStoreService _kvStoreService;
        
        public StorageBackgroundService(IStoragePersistenceService persistenceService, IKeyValueStoreService kvStoreService)
        {
            _persistenceService = persistenceService;
            _kvStoreService = kvStoreService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                int storeCount = _kvStoreService.GetStoreCount();
                if (storeCount >= _maxStoreSize)                
                    _persistenceService.FlushStoreToSSTable();
                

                int sstCount = _persistenceService.GetSSTableCount();
                if (sstCount > _maxSSTFiles)                
                    _persistenceService.CompactSSTables(_sstCompactionBatchSize);
                
                await Task.Delay(_flushInterval, stoppingToken);
            }
        }
    }
}
