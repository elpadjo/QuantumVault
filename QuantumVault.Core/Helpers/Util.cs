using Microsoft.AspNetCore.Http;

namespace QuantumVault.Core.Helpers
{
    public static class Util
    {        
        public static async Task<IResult> HandleRequestAsync(Func<Task<IResult>> action)
        {
            try
            {
                return await action();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return Results.Problem("An unexpected error occurred.");
            }
        }
        


    }
}

