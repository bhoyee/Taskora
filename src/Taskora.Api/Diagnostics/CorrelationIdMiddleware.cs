using Microsoft.Extensions.Primitives;

namespace TodoApp.Api.Diagnostics;

internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

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
