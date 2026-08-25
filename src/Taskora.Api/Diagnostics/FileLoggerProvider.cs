using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TodoApp.Api.Diagnostics;

/// <summary>Configuration for <see cref="FileLoggerProvider"/>.</summary>
public sealed class FileLoggerOptions
{
    public bool Enabled { get; set; } = true;

    public string Directory { get; set; } = "App_Data/logs";

    public int RetentionDays { get; set; } = 30;
}

/// <summary>
/// A dependency-free <see cref="ILoggerProvider"/> that appends structured
/// (JSON Lines) log entries to a daily rolling file under
/// <see cref="FileLoggerOptions.Directory"/> and prunes files older than
/// <see cref="FileLoggerOptions.RetentionDays"/> on every write.
/// </summary>
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

    /// <summary>Creates a logger that funnels entries back through this provider's <see cref="Write"/>.</summary>
    public ILogger CreateLogger(string categoryName) =>
        new FileLogger(categoryName, this);

    /// <summary>No unmanaged resources are held; file handles are opened and closed per write.</summary>
    public void Dispose()
    {
    }

    /// <summary>Receives the external scope provider (e.g. for correlation IDs) from the logging infrastructure.</summary>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    // Serializes one log entry as a JSON line and appends it to today's log
    // file, then prunes expired files. Skipped entirely when logging is
    // disabled or the level is below Information.
    private void Write(
        string category,
        LogLevel level,
        EventId eventId,
        string message,
        Exception? exception)
    {
        // Keep this logger dependency-free so it works on Render, local
        // development, and simple portfolio hosting without extra services.
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

    // Deletes any "taskora-*.jsonl" file whose last write time is older than
    // the configured retention window, based on the given "current" time.
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

    // ILogger implementation returned per category; delegates the actual
    // file write back to the owning provider so all categories share one
    // lock and one rolling file.
    private sealed class FileLogger(
        string categoryName,
        FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            provider._scopeProvider.Push(state);

        // File logging only records Information and above.
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
