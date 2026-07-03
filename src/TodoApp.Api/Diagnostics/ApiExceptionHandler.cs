using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TodoApp.Api.Diagnostics;

internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
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
