using System.Collections.Concurrent;
using System.Threading.Channels;

namespace TodoApp.Api.Realtime;

/// <summary>
/// In-memory pub/sub hub for workspace realtime events (e.g. task/project
/// changes) used to fan out updates to connected clients (such as SSE/long-poll
/// subscribers) without any external message broker. Subscribers are keyed
/// per workspace, and each gets its own bounded channel.
/// </summary>
public sealed class WorkspaceEventBroadcaster
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<WorkspaceRealtimeEvent>>> _subscribers = new();

    /// <summary>
    /// Registers a new subscriber for the given workspace and returns a
    /// subscription exposing a bounded channel reader (capacity 50, drops
    /// the oldest event on overflow) plus an unsubscribe callback that is
    /// invoked when the returned subscription is disposed.
    /// </summary>
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

    /// <summary>
    /// Publishes an event to every current subscriber of the given
    /// workspace. A no-op if the workspace has no subscribers. Writes are
    /// best-effort (<c>TryWrite</c>) against each subscriber's bounded
    /// channel, so a slow/full subscriber loses its oldest queued event
    /// rather than blocking the publisher.
    /// </summary>
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

    // Completes and removes the given subscriber's channel, and drops the
    // workspace entry entirely once it has no remaining subscribers.
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

/// <summary>A single realtime event broadcast to workspace subscribers (e.g. an entity created/updated/deleted).</summary>
public sealed record WorkspaceRealtimeEvent(
    string EventType,
    Guid WorkspaceId,
    string EntityType,
    Guid? EntityId,
    Guid? ActorId,
    DateTimeOffset OccurredAt);

/// <summary>
/// A live handle to one subscriber's event stream from
/// <see cref="WorkspaceEventBroadcaster.Subscribe"/>. Disposing it
/// unregisters the subscriber and completes its channel.
/// </summary>
public sealed class WorkspaceEventSubscription(
    Guid workspaceId,
    Guid subscriberId,
    ChannelReader<WorkspaceRealtimeEvent> reader,
    Action<Guid, Guid> unsubscribe)
    : IAsyncDisposable
{
    /// <summary>The channel reader consumers should await for incoming events.</summary>
    public ChannelReader<WorkspaceRealtimeEvent> Reader { get; } = reader;

    /// <summary>Unsubscribes from the broadcaster, releasing this subscriber's channel.</summary>
    public ValueTask DisposeAsync()
    {
        unsubscribe(workspaceId, subscriberId);
        return ValueTask.CompletedTask;
    }
}
