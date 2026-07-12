using System.Collections.Concurrent;

namespace TodoApp.Api.Diagnostics;

public sealed class InMemoryLogStore
{
    private const int MaxEntries = 200;
    private readonly ConcurrentQueue<OperationLogEntry> _entries = new();

    public void Add(OperationLogEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyCollection<OperationLogEntry> Recent(int count = 50) =>
        _entries
            .Reverse()
            .Take(count)
            .ToArray();
}

public sealed record OperationLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception,
    string? EventId);
