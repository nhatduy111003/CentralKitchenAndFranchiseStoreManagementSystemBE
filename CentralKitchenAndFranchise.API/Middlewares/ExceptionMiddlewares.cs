using System.Net;
using CentralKitchenAndFranchise.DTO.Responses;

namespace CentralKitchenAndFranchise.API.Middlewares;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            await Write(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await Write(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (ArgumentException ex)
        {
            await Write(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await Write(context, HttpStatusCode.Conflict, ex.Message);
        }
        catch (Exception ex)
        {
            await Write(context, HttpStatusCode.InternalServerError, "Internal server error.", ex.Message);
        }
    }

    private static async Task Write(HttpContext ctx, HttpStatusCode code, string message, string? detail = null)
    {
        ctx.Response.StatusCode = (int)code;
        ctx.Response.ContentType = "application/json";

        var errors = detail is null ? null : new List<string> { detail };
        await ctx.Response.WriteAsJsonAsync(ApiResponse.Fail(message, errors));
    }
}
