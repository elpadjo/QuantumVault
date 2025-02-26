using QuantumVault.Core.Models;
using QuantumVault.Infrastructure.Persistence;

namespace QuantumVault.Api.Endpoints
{
    public static class StoreEndpoints
    {
        public static void MapStoreEndpoints(this WebApplication app)
        {
            app.MapGet("/quantumvault/default", () =>
            {
                return Results.Ok("Quantum Vault Service Launch Completed. You can now access all endpoints.");
            });

            app.MapPost("/quantumvault/put", async (IKeyValueStoreService storeService, HttpContext context) =>
            {
                var data = await context.Request.ReadFromJsonAsync<KeyValueModel>();
                if (data is null) return Results.BadRequest();

                await storeService.PutAsync(data.Key, data.Value);
                return Results.Ok(new { message = "Key stored successfully" });
            });

            app.MapGet("/quantumvault/read", async (IKeyValueStoreService storeService, string key) =>
            {
                var value = await storeService.ReadAsync(key);
                return value is not null ? Results.Ok(new { key, value }) : Results.NotFound();
            });

            app.MapDelete("/quantumvault/delete", async (IKeyValueStoreService storeService, string key) =>
            {
                await storeService.DeleteAsync(key);
                return Results.Ok(new { message = "Key deleted successfully" });
            });

            app.MapGet("/quantumvault/range", async (IKeyValueStoreService storeService, string startKey, string endKey) =>
            {
                var results = await storeService.ReadKeyRangeAsync(startKey, endKey);
                return Results.Ok(results);
            });

            app.MapPost("/quantumvault/batchput", async (IKeyValueStoreService storeService, Dictionary<string, string> keyValues) =>
            {
                var results = await storeService.BatchPutAsync(keyValues);
                return Results.Ok(results);
            });
        }
    }
}
