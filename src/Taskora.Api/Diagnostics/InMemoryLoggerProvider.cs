using Microsoft.Extensions.Logging;

namespace TodoApp.Api.Diagnostics;

public sealed class InMemoryLoggerProvider(InMemoryLogStore store)
    : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopeProvider =
        new LoggerExternalScopeProvider();

    public ILogger CreateLogger(string categoryName) =>
        new InMemoryLogger(categoryName, store, this);

    public void Dispose()
    {
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    private sealed class InMemoryLogger(
        string categoryName,
        InMemoryLogStore store,
        InMemoryLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            provider._scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information;

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
