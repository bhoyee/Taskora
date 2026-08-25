using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TodoApp.Api.Diagnostics;

/// <summary>
/// Global unhandled-exception handler (registered via
/// <c>AddExceptionHandler&lt;ApiExceptionHandler&gt;</c> and <c>UseExceptionHandler()</c>
/// in Program.cs) that converts any exception which escapes an endpoint into
/// a ProblemDetails HTTP response instead of a raw 500/stack trace.
/// </summary>
internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    /// <summary>
    /// Logs the exception (unless it's a client-caused <see cref="BadHttpRequestException"/>)
    /// and writes a ProblemDetails response: 400 with a generic "malformed
    /// request" message for bad request bodies, or 500 with a generic
    /// "unexpected error" message for everything else. Always returns true,
    /// indicating the exception was handled.
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Bad JSON/body errors are expected client mistakes; unexpected server
        // failures are logged with the active correlation id scope.
        if (exception is not BadHttpRequestException)
        {
            logger.LogError(
                exception,
                "Unhandled request failure for {Path}",
                httpContext.Request.Path);
        }

        var statusCode = exception is BadHttpRequestException
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;

        httpContext.Response.StatusCode = statusCode;
        return await problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = statusCode == StatusCodes.Status400BadRequest
                        ? "Malformed request"
                        : "An unexpected error occurred",
                    Detail = statusCode == StatusCodes.Status400BadRequest
                        ? "The request body could not be read."
                        : "The request could not be completed."
                }
            });
    }
}
