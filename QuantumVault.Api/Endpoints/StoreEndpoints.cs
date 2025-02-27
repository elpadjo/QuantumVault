using Microsoft.AspNetCore.Mvc;
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

            app.MapPost("/quantumvault/put", async ([FromServices] IKeyValueStoreService storeService, [FromBody] KeyValueModel request) =>
            {
                try
                {
                    await storeService.PutAsync(request.Key, request.Value);
                    return Results.Ok(new { message = "Key stored successfully" });
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

            app.MapGet("/quantumvault/read/{key}", async ([FromServices] IKeyValueStoreService storeService, [FromRoute] string key) =>
            {                
                try
                {
                    var value = await storeService.ReadAsync(key);
                    return value is not null ? Results.Ok(new { key, value }) : Results.NotFound();
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

            app.MapDelete("/quantumvault/delete/{key}", async ([FromServices] IKeyValueStoreService storeService, [FromRoute] string key) =>
            {                
                try
                {
                    await storeService.DeleteAsync(key);
                    return Results.Ok(new { message = "Key deleted successfully" });
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

            app.MapGet("/quantumvault/range", async ([FromServices] IKeyValueStoreService storeService, [FromQuery] string startKey, [FromQuery] string endKey) =>
            {                
                try
                {
                    var values = await storeService.ReadKeyRangeAsync(startKey, endKey);
                    return values.Count > 0 ? Results.Ok(values) : Results.NotFound("No keys found in range.");
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

            app.MapPost("/quantumvault/batchput", async ([FromServices] IKeyValueStoreService storeService, [FromBody] KeyValueBatchModel request) =>
            {                
                try
                {
                    var results = await storeService.BatchPutAsync(request.KeyValues);
                    return Results.Ok(results);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });
        }
    }
}
