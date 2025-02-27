using QuantumVault.Api.Endpoints;
using QuantumVault.Core.Services;
using QuantumVault.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<IKeyValueStoreService, KeyValueStoreService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
}

// Map endpoints
app.MapStoreEndpoints();

app.UseHttpsRedirection();
app.Run();

public partial class Program { }




