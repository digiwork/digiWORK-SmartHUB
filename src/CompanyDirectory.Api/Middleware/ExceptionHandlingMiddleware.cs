using CompanyDirectory.Shared.Errors;
using System.Text.Json;

namespace CompanyDirectory.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorResponse(context, ex);
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, error) = ex switch
        {
            ArgumentException e => (StatusCodes.Status400BadRequest,
                ApiError.From("BAD_REQUEST", e.Message)),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden,
                ApiError.From("FORBIDDEN", "Access denied.")),
            KeyNotFoundException => (StatusCodes.Status404NotFound,
                ApiError.From("NOT_FOUND", "Resource not found.")),
            OperationCanceledException => (StatusCodes.Status408RequestTimeout,
                ApiError.From("REQUEST_TIMEOUT", "Request was cancelled.")),
            _ => (StatusCodes.Status500InternalServerError,
                ApiError.From("INTERNAL_ERROR", "An unexpected error occurred."))
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(error, JsonOptions));
    }
}
