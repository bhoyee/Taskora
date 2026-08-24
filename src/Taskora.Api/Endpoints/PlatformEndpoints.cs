using TodoApp.Api.Authorization;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Platform;

namespace TodoApp.Api.Endpoints;

internal static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/platform")
            .WithTags("Platform")
            .RequireAuthorization();

        group.MapGet("/workspaces", ListWorkspacesAsync)
            .WithName("ListPlatformWorkspaces")
            .Produces<IReadOnlyList<PlatformWorkspaceSummary>>()
            .Produces(StatusCodes.Status403Forbidden);
        group.MapGet("/workspaces/{workspaceId:guid}", GetWorkspaceDetailAsync)
            .WithName("GetPlatformWorkspaceDetail")
            .Produces<PlatformWorkspaceDetailDto>()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> ListWorkspacesAsync(
        ListPlatformWorkspacesHandler handler,
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!await SuperAdminAuthorization.IsSuperAdminAsync(
                currentUser, accounts, configuration, cancellationToken))
        {
            return Results.Forbid();
        }

        return ApiResult.From(await handler.HandleAsync(
            new ListPlatformWorkspacesQuery(),
            cancellationToken));
    }

    private static async Task<IResult> GetWorkspaceDetailAsync(
        Guid workspaceId,
        GetPlatformWorkspaceDetailHandler handler,
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!await SuperAdminAuthorization.IsSuperAdminAsync(
                currentUser, accounts, configuration, cancellationToken))
        {
            return Results.Forbid();
        }

        return ApiResult.From(await handler.HandleAsync(
            new GetPlatformWorkspaceDetailQuery(workspaceId),
            cancellationToken));
    }
}
