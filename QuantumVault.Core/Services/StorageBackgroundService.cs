using Microsoft.Extensions.Hosting;
using QuantumVault.Infrastructure.Persistence;

namespace QuantumVault.Core.Services
{
    public class StorageBackgroundService : BackgroundService
    {
        private readonly int _flushInterval = 1000 * 60 * 1000; // 1000 minutes
        private readonly int _maxStoreSize = 100; // Flush when size reaches 100

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
                int store = _kvStoreService.GetStoreCount();
                if (store >= _maxStoreSize)
                {
                    _persistenceService.FlushStoreToSSTable();
                }

                _persistenceService.CompactSSTables(); // Periodic compaction
                await Task.Delay(_flushInterval, stoppingToken);
            }
        }
    }
}
