using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TodoApp.Api.Diagnostics;

public sealed class FileLoggerOptions
{
    public bool Enabled { get; set; } = true;

    public string Directory { get; set; } = "App_Data/logs";

    public int RetentionDays { get; set; } = 30;
}

public sealed class FileLoggerProvider(FileLoggerOptions options)
    : ILoggerProvider, ISupportExternalScope
{
    private readonly object _sync = new();
    private readonly string _directory = Path.GetFullPath(
        string.IsNullOrWhiteSpace(options.Directory)
            ? "App_Data/logs"
            : options.Directory);
    private IExternalScopeProvider _scopeProvider =
        new LoggerExternalScopeProvider();

    public ILogger CreateLogger(string categoryName) =>
        new FileLogger(categoryName, this);

    public void Dispose()
    {
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    private void Write(
        string category,
        LogLevel level,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (!options.Enabled || level < LogLevel.Information)
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        var now = DateTimeOffset.UtcNow;
        var entry = new OperationLogEntry(
            now,
            level.ToString(),
            category,
            message,
            exception?.ToString(),
            eventId.Id == 0 ? null : eventId.Id.ToString(),
            ScopeReader.FindCorrelationId(_scopeProvider));
        var path = Path.Combine(
            _directory,
            $"taskora-{now:yyyyMMdd}.jsonl");

        lock (_sync)
        {
            File.AppendAllText(
                path,
                JsonSerializer.Serialize(entry) + Environment.NewLine);
            PruneOldFiles(now);
        }
    }

    private void PruneOldFiles(DateTimeOffset now)
    {
        var retentionDays = Math.Max(1, options.RetentionDays);
        var cutoff = now.AddDays(-retentionDays);
        foreach (var file in System.IO.Directory.EnumerateFiles(
            _directory,
            "taskora-*.jsonl"))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTimeUtc < cutoff.UtcDateTime)
            {
                info.Delete();
            }
        }
    }

    private sealed class FileLogger(
        string categoryName,
        FileLoggerProvider provider) : ILogger
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

            provider.Write(
                categoryName,
                logLevel,
                eventId,
                formatter(state, exception),
                exception);
        }
    }
}
