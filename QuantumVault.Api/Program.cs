using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using QuantumVault.Api.Endpoints;
using QuantumVault.Core.Services;
using QuantumVault.Services.Interfaces;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Get certificate path and password from environment variables
// var certPath = builder.Configuration["ASPNETCORE_Kestrel__Certificates__Default__Path"] ?? "/https/cert.pfx";
// var certPassword = builder.Configuration["ASPNETCORE_Kestrel__Certificates__Default__Password"];

// Register services
builder.Services.AddSingleton<IKeyValueStoreService, KeyValueStoreService>();
builder.Services.AddSingleton<IStoragePersistenceService, StoragePersistenceService>();
builder.Services.AddHostedService<SnapshotBackgroundService>();

/*
// Configure Kestrel for HTTPS with HTTP fallback
builder.WebHost.ConfigureKestrel(options =>
{
    if (File.Exists(certPath))
    {
        try
        {
            options.ListenAnyIP(443, listenOptions => listenOptions.UseHttps(certPath, certPassword));
            Console.WriteLine("HTTPS enabled on port 443.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading certificate: {ex.Message}. Falling back to HTTP.");
            options.ListenAnyIP(8080); // Fallback to HTTP
        }
    }
    else
    {
        Console.WriteLine("Warning: HTTPS certificate not found. Running on HTTP only.");
        options.ListenAnyIP(8080); // Default to HTTP
    }
});*/

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

app.Run();

public partial class Program { }
