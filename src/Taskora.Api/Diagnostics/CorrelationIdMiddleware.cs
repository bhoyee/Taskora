using Microsoft.Extensions.Primitives;

namespace TodoApp.Api.Diagnostics;

/// <summary>
/// Middleware that ensures every request has a correlation id: it reuses a
/// caller-supplied <c>X-Correlation-ID</c> header when present, otherwise falls
/// back to the ASP.NET request id. The id is echoed back on the response header
/// and pushed into the logging scope so it shows up in all logs for the request.
/// </summary>
internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>
    /// Resolves the correlation id for the current request, stamps it onto the
    /// trace identifier and response header, opens a logging scope carrying it,
    /// and invokes the rest of the pipeline.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Accept a caller-supplied trace id when present; otherwise use the
        // ASP.NET request id so every request has something searchable in logs.
        var correlationId =
            context.Request.Headers.TryGetValue(
                HeaderName,
                out StringValues supplied) &&
            !StringValues.IsNullOrEmpty(supplied)
                ? supplied.ToString()
                : context.TraceIdentifier;

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // The logging scope is picked up by the in-memory and file loggers.
        using (logger.BeginScope(
                   new Dictionary<string, object>
                   {
                       ["CorrelationId"] = correlationId
                   }))
        {
            await next(context);
        }
    }
}
