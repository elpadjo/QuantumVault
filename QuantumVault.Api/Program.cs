using QuantumVault.Api.Endpoints;
using QuantumVault.Core.Services;
using QuantumVault.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<IKeyValueStoreService, KeyValueStoreService>();
builder.Services.AddSingleton<IStoragePersistenceService, StoragePersistenceService>();
builder.Services.AddHostedService<SnapshotBackgroundService>();

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

// Map endpoints
app.MapStoreEndpoints();

app.Run();

public partial class Program { }
