using TodoApp.Api.Authorization;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Platform;

namespace TodoApp.Api.Endpoints;

/// <summary>
/// Registers cross-workspace platform administration routes under
/// "/api/v1/platform". Every route requires an authenticated caller and is
/// further gated to super-admins via
/// <see cref="SuperAdminAuthorization.IsSuperAdminAsync"/> (account email
/// matched against the configured super-admin email list), since these
/// endpoints expose data across all workspaces rather than just the
/// caller's own.
/// </summary>
internal static class PlatformEndpoints
{
    /// <summary>
    /// Maps the platform endpoint group. All routes require authentication;
    /// handlers additionally enforce super-admin access before returning data.
    /// </summary>
    public static IEndpointRouteBuilder MapPlatformEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/platform")
            .WithTags("Platform")
            .RequireAuthorization();

        // GET /api/v1/platform/workspaces
        // Super-admin only. Lists a summary of every workspace on the platform
        // (200 list of PlatformWorkspaceSummary, 403 for non super-admins).
        group.MapGet("/workspaces", ListWorkspacesAsync)
            .WithName("ListPlatformWorkspaces")
            .Produces<IReadOnlyList<PlatformWorkspaceSummary>>()
            .Produces(StatusCodes.Status403Forbidden);
        // GET /api/v1/platform/workspaces/{workspaceId}
        // Super-admin only. Returns detailed information for a single
        // workspace (200 PlatformWorkspaceDetailDto, 403 for non
        // super-admins, 404 if the workspace does not exist).
        group.MapGet("/workspaces/{workspaceId:guid}", GetWorkspaceDetailAsync)
            .WithName("GetPlatformWorkspaceDetail")
            .Produces<PlatformWorkspaceDetailDto>()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    // Handler for GET /api/v1/platform/workspaces. Gated to super-admins;
    // delegates to ListPlatformWorkspacesHandler.
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

    // Handler for GET /api/v1/platform/workspaces/{workspaceId}. Gated to
    // super-admins; delegates to GetPlatformWorkspaceDetailHandler.
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
