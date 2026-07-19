using System.Collections.Concurrent;

namespace TodoApp.Api.Diagnostics;

public sealed class InMemoryLogStore
{
    private readonly ConcurrentQueue<OperationLogEntry> _entries = new();

    public InMemoryLogStore(int maxEntries = 200, int retentionDays = 30)
    {
        MaxEntries = Math.Max(1, maxEntries);
        RetentionDays = Math.Max(1, retentionDays);
    }

    public int MaxEntries { get; }

    public int RetentionDays { get; }

    public void Add(OperationLogEntry entry)
    {
        _entries.Enqueue(entry);
        Prune(DateTimeOffset.UtcNow);
    }

    public IReadOnlyCollection<OperationLogEntry> Recent(int count = 50)
    {
        Prune(DateTimeOffset.UtcNow);

        return _entries
            .Reverse()
            .Take(count)
            .ToArray();
    }

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

public sealed record OperationLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception,
    string? EventId,
    string? CorrelationId);
