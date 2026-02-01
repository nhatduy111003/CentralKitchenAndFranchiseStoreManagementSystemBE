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
            await Write(context, HttpStatusCode.Unauthorized, ex.Message, "UNAUTHORIZED");
        }
        catch (KeyNotFoundException ex)
        {
            await Write(context, HttpStatusCode.NotFound, ex.Message, "NOT_FOUND");
        }
        catch (ArgumentException ex)
        {
            await Write(context, HttpStatusCode.BadRequest, ex.Message, "BAD_REQUEST");
        }
        catch (InvalidOperationException ex)
        {
            await Write(context, HttpStatusCode.Conflict, ex.Message, "CONFLICT");
        }
        catch (Exception ex)
        {
            await Write(context, HttpStatusCode.InternalServerError, "Internal server error.", "INTERNAL_ERROR",
                errors: new List<string> { ex.Message });
        }
    }

    private static async Task Write(
        HttpContext ctx,
        HttpStatusCode code,
        string message,
        string errorCode,
        List<string>? errors = null,
        Dictionary<string, string[]>? fieldErrors = null)
    {
        if (ctx.Response.HasStarted) return;

        ctx.Response.StatusCode = (int)code;
        ctx.Response.ContentType = "application/json";

        var resp = ApiResponse.Fail(message, errors, errorCode, fieldErrors);
        await ctx.Response.WriteAsJsonAsync(resp);
    }
}
