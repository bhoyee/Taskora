using TodoApp.Api.Contracts;
using TodoApp.Application.Collaboration;

namespace TodoApp.Api.Endpoints;

internal static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/workspaces")
            .WithTags("Workspaces")
            .RequireAuthorization();

        group.MapGet("/", async (
            GetMyWorkspacesHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new GetMyWorkspacesQuery(),
                cancellationToken)));
        group.MapGet("/{workspaceId:guid}/members", async (
            Guid workspaceId,
            GetWorkspaceMembersHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new GetWorkspaceMembersQuery(workspaceId),
                cancellationToken)));
        group.MapPost("/{workspaceId:guid}/members", async (
            Guid workspaceId,
            AddWorkspaceMemberRequest request,
            AddWorkspaceMemberHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new AddWorkspaceMemberCommand(
                    workspaceId,
                    request.Email,
                    request.Role),
                cancellationToken)));
        group.MapPut("/{workspaceId:guid}/members/{userId:guid}", async (
            Guid workspaceId,
            Guid userId,
            ChangeWorkspaceRoleRequest request,
            ChangeWorkspaceRoleHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new ChangeWorkspaceRoleCommand(
                    workspaceId,
                    userId,
                    request.Role),
                cancellationToken)));
        group.MapDelete("/{workspaceId:guid}/members/{userId:guid}", async (
            Guid workspaceId,
            Guid userId,
            RemoveWorkspaceMemberHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new RemoveWorkspaceMemberCommand(workspaceId, userId),
                cancellationToken)));

        return endpoints;
    }
}
