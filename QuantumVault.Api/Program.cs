using QuantumVault.Api.Endpoints;
using QuantumVault.Core.Services;
using QuantumVault.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
//var certPath = builder.Configuration["ASPNETCORE_Kestrel__Certificates__Default__Path"];
//var certPassword = builder.Configuration["ASPNETCORE_Kestrel__Certificates__Default__Password"];

var certPath = "/app/cert.pfx"; // Path inside the container
var certPassword = builder.Configuration["ASPNETCORE_Kestrel__Certificates__Default__Password"];


// Register services
builder.Services.AddSingleton<IKeyValueStoreService, KeyValueStoreService>();
builder.Services.AddSingleton<IStoragePersistenceService, StoragePersistenceService>();

/*builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080); // HTTP
    if (File.Exists(certPath))
    {
        options.ListenAnyIP(8081, listenOptions => listenOptions.UseHttps(certPath, certPassword));
    }
});*/

Console.WriteLine($"Certificate Path: {certPath}");
Console.WriteLine($"Certificate Exists: {File.Exists(certPath)}");

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
//app.UseHttpsRedirection();

app.Run();

public partial class Program { }




