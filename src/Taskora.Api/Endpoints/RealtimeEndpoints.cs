using System.Text.Json;
using TodoApp.Api.Realtime;
using TodoApp.Application.Abstractions;

namespace TodoApp.Api.Endpoints;

/// <summary>
/// Registers the Server-Sent Events (SSE) route that streams live workspace
/// activity to connected clients.
/// </summary>
internal static class RealtimeEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>Maps the realtime workspace event stream route.</summary>
    public static IEndpointRouteBuilder MapRealtimeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        // GET /api/v1/workspaces/{workspaceId}/events
        // Requires authentication. Opens a long-lived text/event-stream
        // connection and pushes workspace notifications as they occur; the
        // caller must be a member of the workspace
        // (200 SSE stream kept open until the client disconnects, 404 if the
        // workspace does not exist or the caller is not a member).
        endpoints.MapGet(
                "/api/v1/workspaces/{workspaceId:guid}/events",
                StreamWorkspaceEventsAsync)
            .WithTags("Realtime")
            .RequireAuthorization()
            .WithName("StreamWorkspaceEvents")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    // Handler for GET /api/v1/workspaces/{workspaceId}/events. Verifies
    // workspace membership, then subscribes to the broadcaster and streams
    // each notification to the client as an SSE "event"/"data" pair until
    // the request is cancelled (client disconnects).
    private static async Task<IResult> StreamWorkspaceEventsAsync(
        Guid workspaceId,
        ICurrentUser currentUser,
        IWorkspaceRepository workspaces,
        WorkspaceEventBroadcaster broadcaster,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaces.GetByIdAsync(
            workspaceId,
            cancellationToken);
        if (workspace is null || !workspace.HasMember(currentUser.UserId))
        {
            return Results.NotFound();
        }

        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";

        await using var subscription = broadcaster.Subscribe(workspaceId);
        await response.WriteAsync(": connected\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);

        await foreach (var notification in subscription.Reader.ReadAllAsync(
            cancellationToken))
        {
            await response.WriteAsync(
                $"event: {notification.EventType}\n",
                cancellationToken);
            await response.WriteAsync(
                $"data: {JsonSerializer.Serialize(notification, JsonOptions)}\n\n",
                cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }

        return Results.Empty;
    }
}
