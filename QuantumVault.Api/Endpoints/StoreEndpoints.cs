using Microsoft.AspNetCore.Mvc;
using QuantumVault.Core.Enums;
using QuantumVault.Core.Helpers;
using QuantumVault.Core.Models;
using QuantumVault.Services.Interfaces;

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
                return await Util.HandleRequestAsync(async () =>
                {
                    if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Value))
                        return Results.BadRequest(new { message = "Payload must contain exactly one key-value pair with non-empty values." });

                    var tcs = new TaskCompletionSource<bool>();

                    storeService.EnqueueRequest(RequestPriority.Low, async () =>
                    {
                        try
                        {
                            await storeService.PutAsync(request.Key, request.Value);
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    });

                    await tcs.Task;
                    return Results.Ok(new { message = "Key stored successfully" });
                });
            });

            app.MapGet("/quantumvault/v1/read/{key}", async ([FromServices] IKeyValueStoreService storeService, [FromRoute] string key) =>
            {
                return await Util.HandleRequestAsync(async () =>
                {
                    var value = await storeService.ReadAsync(key);
                    return value is not null ? Results.Ok(new { key, value }) : Results.NotFound();
                });
            });

            app.MapDelete("/quantumvault/v1/delete/{key}", async ([FromServices] IKeyValueStoreService storeService, [FromRoute] string key) =>
            {
                return await Util.HandleRequestAsync(async () =>
                {
                    await storeService.DeleteAsync(key);
                    return Results.Ok(new { message = "Key deleted successfully" });
                });
            });

            app.MapGet("/quantumvault/v1/range", async ([FromServices] IKeyValueStoreService storeService,
                [FromQuery] string startKey,
                [FromQuery] string endKey,
                [FromQuery] int pageSize = 20,
                [FromQuery] int pageNumber = 1) =>
            {
                return await Util.HandleRequestAsync(async () =>
                {
                    var (values, totalItems) = await storeService.ReadKeyRangeAsync(startKey, endKey, pageSize, pageNumber);
                    return values.Count > 0
                        ? Results.Ok(new { PageNumber = pageNumber, PageSize = pageSize, TotalItems = totalItems, Data = values })
                        : Results.NotFound("No keys found in range.");
                });
            });

            app.MapPost("/quantumvault/v1/batchput", async ([FromServices] IKeyValueStoreService storeService, [FromBody] KeyValueBatchModel request) =>
            {
                return await Util.HandleRequestAsync(async () =>
                {
                    var results = await storeService.BatchPutAsync(request.KeyValues);
                    return Results.Ok(results);
                });
            });
        }
    }
}
