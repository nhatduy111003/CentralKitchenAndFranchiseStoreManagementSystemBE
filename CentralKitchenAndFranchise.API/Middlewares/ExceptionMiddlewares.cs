// CentralKitchenAndFranchise.API/Middlewares/ExceptionMiddlewares.cs
using System.Net;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.DTO.Responses;

namespace CentralKitchenAndFranchise.API.Middlewares;

/// <summary>
/// Centralized exception -> ApiResponse mapping.
/// NOTE:
/// - 401 is reserved for unauthenticated / invalid token (UNAUTHORIZED)
/// - 403 is reserved for authenticated but not allowed (FORBIDDEN)
/// - 400 validation errors should use (VALIDATION_ERROR)
/// </summary>
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
        catch (ForbiddenAccessException ex)
        {
            await Write(context, HttpStatusCode.Forbidden, ex.Message, "FORBIDDEN");
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
            await Write(context, HttpStatusCode.BadRequest, ex.Message, "VALIDATION_ERROR");
        }
        catch (InvalidOperationException ex)
        {
            await Write(context, HttpStatusCode.Conflict, ex.Message, "CONFLICT");
        }
        catch (Exception ex)
        {
            await Write(
                context,
                HttpStatusCode.InternalServerError,
                "Internal server error.",
                "INTERNAL_ERROR",
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
