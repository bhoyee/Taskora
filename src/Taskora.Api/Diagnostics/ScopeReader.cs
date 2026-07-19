using Microsoft.Extensions.Logging;

namespace TodoApp.Api.Diagnostics;

internal static class ScopeReader
{
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
