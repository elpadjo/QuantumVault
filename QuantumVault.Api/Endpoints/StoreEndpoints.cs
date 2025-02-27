using Microsoft.AspNetCore.Mvc;
using QuantumVault.Core.Models;
using QuantumVault.Infrastructure.Persistence;

namespace QuantumVault.Api.Endpoints
{
    public static class StoreEndpoints
    {
        public static void MapStoreEndpoints(this WebApplication app)
        {
            app.MapGet("/quantumvault/v1/default", () =>
            {
                return Results.Ok("Quantum Vault Service Launch Completed. You can now access all endpoints.");
            });

            app.MapPost("/quantumvault/v1/put", async ([FromServices] IKeyValueStoreService storeService, [FromBody] KeyValueModel request) =>
            {
                try
                {
                    await storeService.PutAsync(request.Key, request.Value);
                    return Results.Ok(new { message = "Key stored successfully" });

                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (ArgumentException ex)  // Handle invalid input
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (Exception ex)  // Catch-all for unexpected issues
                {
                    return Results.Problem("An unexpected error occurred.");
                }
            });

            app.MapGet("/quantumvault/v1/read/{key}", async ([FromServices] IKeyValueStoreService storeService, [FromRoute] string key) =>
            {                
                try
                {
                    var value = await storeService.ReadAsync(key);
                    return value is not null ?
                    Results.Ok(new { key, value }) : Results.NotFound();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (KeyNotFoundException)  // Handle missing keys
                {
                    return Results.NotFound(new { message = "Key not found" });
                }
                catch (ArgumentException ex)  // Handle invalid input
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (Exception)  // Catch-all for unexpected issues
                {
                    return Results.Problem("An unexpected error occurred.");
                }
            });

            app.MapDelete("/quantumvault/v1/delete/{key}", async ([FromServices] IKeyValueStoreService storeService, [FromRoute] string key) =>
            {                
                try
                {
                    await storeService.DeleteAsync(key);
                    return Results.Ok(new { message = "Key deleted successfully" });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (KeyNotFoundException)  // Handle missing keys
                {
                    return Results.NotFound(new { message = "Key not found" });
                }
                catch (ArgumentException ex)  // Handle invalid input
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (Exception)  // Catch-all for unexpected issues
                {
                    return Results.Problem("An unexpected error occurred.");
                }
            });

            app.MapGet("/quantumvault/v1/range", async ([FromServices] IKeyValueStoreService storeService, [FromQuery] string startKey, [FromQuery] string endKey) =>
            {                
                try
                {
                    var values = await storeService.ReadKeyRangeAsync(startKey, endKey);
                    return values.Count > 0 ? Results.Ok(values) : Results.NotFound("No keys found in range.");
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (KeyNotFoundException)  // Handle missing keys
                {
                    return Results.NotFound(new { message = "Key not found" });
                }
                catch (ArgumentException ex)  // Handle invalid input
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (Exception)  // Catch-all for unexpected issues
                {
                    return Results.Problem("An unexpected error occurred.");
                }
            });

            app.MapPost("/quantumvault/v1/batchput", async ([FromServices] IKeyValueStoreService storeService, [FromBody] KeyValueBatchModel request) =>
            {                
                try
                {
                    var results = await storeService.BatchPutAsync(request.KeyValues);
                    return Results.Ok(results);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (ArgumentException ex)  // Handle invalid input
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (Exception)  // Catch-all for unexpected issues
                {
                    return Results.Problem("An unexpected error occurred.");
                }
            });
        }
    }
}
