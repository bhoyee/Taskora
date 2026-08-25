using System.Collections.Concurrent;

namespace TodoApp.Api.Diagnostics;

/// <summary>
/// Thread-safe, bounded, in-memory ring buffer of recent log entries backing
/// the super-admin Operations UI's live log view. Registered as a singleton
/// and fed by <see cref="InMemoryLoggerProvider"/>; entries beyond
/// <see cref="MaxEntries"/> or older than <see cref="RetentionDays"/> are pruned.
/// </summary>
public sealed class InMemoryLogStore
{
    private readonly ConcurrentQueue<OperationLogEntry> _entries = new();

    /// <summary>
    /// Creates the store with the given capacity/retention, each clamped to
    /// a minimum of 1 to avoid a degenerate empty buffer.
    /// </summary>
    public InMemoryLogStore(int maxEntries = 200, int retentionDays = 30)
    {
        MaxEntries = Math.Max(1, maxEntries);
        RetentionDays = Math.Max(1, retentionDays);
    }

    public int MaxEntries { get; }

    public int RetentionDays { get; }

    /// <summary>Records a new log entry and prunes the buffer to its configured limits.</summary>
    public void Add(OperationLogEntry entry)
    {
        _entries.Enqueue(entry);
        Prune(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> of the most recently added
    /// entries, newest first, after pruning expired/excess entries.
    /// </summary>
    public IReadOnlyCollection<OperationLogEntry> Recent(int count = 50)
    {
        Prune(DateTimeOffset.UtcNow);

        return _entries
            .Reverse()
            .Take(count)
            .ToArray();
    }

    // Drops entries older than the retention window, then trims from the
    // front until the buffer is back within MaxEntries.
    private void Prune(DateTimeOffset now)
    {
        var cutoff = now.AddDays(-RetentionDays);
        while (_entries.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            _entries.TryDequeue(out _);
        }

        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
        {
        }
    }
}

/// <summary>A single captured log line, as displayed in the Operations UI's live log view.</summary>
public sealed record OperationLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception,
    string? EventId,
    string? CorrelationId);
