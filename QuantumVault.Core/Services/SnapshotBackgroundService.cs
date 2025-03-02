using Microsoft.Extensions.Hosting;
using QuantumVault.Services.Interfaces;

namespace QuantumVault.Core.Services
{
    public class SnapshotBackgroundService : BackgroundService
    {
        private readonly IStoragePersistenceService _persistenceService;

        private readonly int _saveInterval = int.Parse(Environment.GetEnvironmentVariable("SAVE_INTERVAL") ?? "1");

        public SnapshotBackgroundService(IStoragePersistenceService persistenceService)
        {
            _persistenceService = persistenceService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                SaveData();
                await Task.Delay(TimeSpan.FromMinutes(_saveInterval), stoppingToken);
            }
        }

        private void SaveData()
        {
            Console.WriteLine($"Saving data at {DateTime.Now}");

            _persistenceService.SaveData();
        }
    }
}
