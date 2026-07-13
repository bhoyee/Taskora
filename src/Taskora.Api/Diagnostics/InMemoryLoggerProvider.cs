using Microsoft.Extensions.Logging;

namespace TodoApp.Api.Diagnostics;

public sealed class InMemoryLoggerProvider(InMemoryLogStore store)
    : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) =>
        new InMemoryLogger(categoryName, store);

    public void Dispose()
    {
    }

    private sealed class InMemoryLogger(
        string categoryName,
        InMemoryLogStore store) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

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
                eventId.Id == 0 ? null : eventId.Id.ToString()));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
