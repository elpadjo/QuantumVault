using QuantumVault.Api.Endpoints;
using QuantumVault.Core.Services;
using QuantumVault.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<IKeyValueStoreService, KeyValueStoreService>();

var app = builder.Build();

// Map endpoints
app.MapStoreEndpoints();

app.UseHttpsRedirection();
app.Run();




