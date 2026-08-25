using Microsoft.Extensions.Logging;

namespace TodoApp.Api.Diagnostics;

/// <summary>
/// <see cref="ILoggerProvider"/> that feeds every logged message (at
/// Information level or above) into an <see cref="InMemoryLogStore"/>,
/// registered in Program.cs alongside the standard logging providers so the
/// Operations UI can display recent log activity without querying a file/sink.
/// </summary>
public sealed class InMemoryLoggerProvider(InMemoryLogStore store)
    : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopeProvider =
        new LoggerExternalScopeProvider();

    /// <summary>Creates a logger for the given category backed by the shared <see cref="InMemoryLogStore"/>.</summary>
    public ILogger CreateLogger(string categoryName) =>
        new InMemoryLogger(categoryName, store, this);

    public void Dispose()
    {
    }

    /// <summary>Receives the external scope provider (e.g. for correlation id scopes) from the logging infrastructure.</summary>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    // ILogger implementation that appends entries to the shared InMemoryLogStore.
    private sealed class InMemoryLogger(
        string categoryName,
        InMemoryLogStore store,
        InMemoryLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            provider._scopeProvider.Push(state);

        // Only Information-level and above are captured, to keep the bounded buffer useful.
        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information;

        // Formats and records a log entry, including its correlation id (if any is in scope).
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            store.Add(new OperationLogEntry(
                DateTimeOffset.UtcNow,
                logLevel.ToString(),
                categoryName,
                formatter(state, exception),
                exception?.Message,
                eventId.Id == 0 ? null : eventId.Id.ToString(),
                ScopeReader.FindCorrelationId(provider._scopeProvider)));
        }
    }
}
