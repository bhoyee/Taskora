using System.Text.Json;
using TodoApp.Api.Realtime;
using TodoApp.Application.Abstractions;

namespace TodoApp.Api.Endpoints;

internal static class RealtimeEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapRealtimeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
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
