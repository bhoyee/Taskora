using Microsoft.Extensions.Logging;

namespace TodoApp.Api.Diagnostics;

/// <summary>
/// Helper for reading well-known values out of the ambient logging scope stack.
/// </summary>
internal static class ScopeReader
{
    /// <summary>
    /// Walks the active logging scopes (innermost first, via <see cref="IExternalScopeProvider"/>)
    /// looking for a "CorrelationId" entry, e.g. as set by <c>CorrelationIdMiddleware</c>.
    /// Returns the first value found, or null if no scope carries one.
    /// </summary>
    public static string? FindCorrelationId(IExternalScopeProvider scopeProvider)
    {
        string? correlationId = null;
        scopeProvider.ForEachScope<object?>((scope, _) =>
        {
            if (correlationId is not null)
            {
                return;
            }

            if (scope is IEnumerable<KeyValuePair<string, object>> values)
            {
                foreach (var item in values)
                {
                    if (item.Key == "CorrelationId" &&
                        item.Value is not null)
                    {
                        correlationId = item.Value.ToString();
                        return;
                    }
                }
            }
        }, state: (object?)null);

        return correlationId;
    }
}
