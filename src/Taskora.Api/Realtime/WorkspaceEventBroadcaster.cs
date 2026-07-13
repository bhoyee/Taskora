using System.Collections.Concurrent;
using System.Threading.Channels;

namespace TodoApp.Api.Realtime;

public sealed class WorkspaceEventBroadcaster
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<WorkspaceRealtimeEvent>>> _subscribers = new();

    public WorkspaceEventSubscription Subscribe(Guid workspaceId)
    {
        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateBounded<WorkspaceRealtimeEvent>(
            new BoundedChannelOptions(50)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

        var subscribers = _subscribers.GetOrAdd(
            workspaceId,
            _ => new ConcurrentDictionary<Guid, Channel<WorkspaceRealtimeEvent>>());
        subscribers[subscriberId] = channel;
        return new WorkspaceEventSubscription(
            workspaceId,
            subscriberId,
            channel.Reader,
            Unsubscribe);
    }

    public ValueTask PublishAsync(
        Guid workspaceId,
        string eventType,
        string entityType,
        Guid? entityId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (!_subscribers.TryGetValue(workspaceId, out var subscribers))
        {
            return ValueTask.CompletedTask;
        }

        var notification = new WorkspaceRealtimeEvent(
            eventType,
            workspaceId,
            entityType,
            entityId,
            actorId == Guid.Empty ? null : actorId,
            DateTimeOffset.UtcNow);

        foreach (var subscriber in subscribers.Values)
        {
            subscriber.Writer.TryWrite(notification);
        }

        return ValueTask.CompletedTask;
    }

    private void Unsubscribe(Guid workspaceId, Guid subscriberId)
    {
        if (!_subscribers.TryGetValue(workspaceId, out var subscribers))
        {
            return;
        }

        if (subscribers.TryRemove(subscriberId, out var channel))
        {
            channel.Writer.TryComplete();
        }

        if (subscribers.IsEmpty)
        {
            _subscribers.TryRemove(workspaceId, out _);
        }
    }
}

public sealed record WorkspaceRealtimeEvent(
    string EventType,
    Guid WorkspaceId,
    string EntityType,
    Guid? EntityId,
    Guid? ActorId,
    DateTimeOffset OccurredAt);

public sealed class WorkspaceEventSubscription(
    Guid workspaceId,
    Guid subscriberId,
    ChannelReader<WorkspaceRealtimeEvent> reader,
    Action<Guid, Guid> unsubscribe)
    : IAsyncDisposable
{
    public ChannelReader<WorkspaceRealtimeEvent> Reader { get; } = reader;

    public ValueTask DisposeAsync()
    {
        unsubscribe(workspaceId, subscriberId);
        return ValueTask.CompletedTask;
    }
}
