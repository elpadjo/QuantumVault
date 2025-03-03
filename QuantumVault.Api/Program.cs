using QuantumVault.Api.Endpoints;
using QuantumVault.Core.Services;
using QuantumVault.Services.Interfaces;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<IKeyValueStoreService, KeyValueStoreService>();
builder.Services.AddSingleton<IStoragePersistenceService, StoragePersistenceService>();
builder.Services.AddHostedService<SnapshotBackgroundService>();
builder.Services.AddHostedService<StorageBackgroundService>();

//Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    int writeLimit = builder.Configuration.GetValue<int>("WRITE_LIMIT");
    int readLimit = builder.Configuration.GetValue<int>("READ_LIMIT");

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = writeLimit, // Limits each IP to 'PermitLimit' requests per minute
            Window = TimeSpan.FromSeconds(1),
            QueueLimit = 5, // Allows up to 5 extra queued requests
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst // Process oldest queued requests first
        });
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
}

//app.UseHttpsRedirection();  // Redirect HTTP to HTTPS (only if HTTPS is available)
app.MapStoreEndpoints();
app.UseRateLimiter(); // Enable Rate Limiting Middleware
app.MapGet("/", () => "Rate limiting enabled!");

app.Run();

public partial class Program { }
