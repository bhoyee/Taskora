using TodoApp.Api.Authorization;
using TodoApp.Api.Contracts;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Collaboration;
using TodoApp.Application.Projects;
using TodoApp.Application.Tasks.Activity;

namespace TodoApp.Api.Endpoints;

/// <summary>
/// Registers the minimal-API endpoints for managing workspaces, their
/// members, invitations, and workspace-scoped projects/activity.
/// </summary>
internal static class WorkspaceEndpoints
{
    /// <summary>
    /// Maps the "/api/v1/workspaces" route group (all requiring an
    /// authenticated user) and the public "/api/v1/invitations" group used to
    /// look up and act on an invitation by its token.
    /// </summary>
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/workspaces")
            .WithTags("Workspaces")
            .RequireAuthorization();

        // GET /api/v1/workspaces: lists the workspaces the current user belongs to.
        // Auth: any authenticated user. Returns: 200 with the caller's workspaces.
        group.MapGet("/", async (
            GetMyWorkspacesHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new GetMyWorkspacesQuery(),
                cancellationToken)));
        // POST /api/v1/workspaces: creates a new workspace owned by the current user.
        // Auth: any authenticated user. Returns: 200 with the created workspace, or a problem response on failure.
        group.MapPost("/", async (
            CreateWorkspaceRequest request,
            CreateWorkspaceHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new CreateWorkspaceCommand(request.Name),
                cancellationToken)));
        // PUT /api/v1/workspaces/{workspaceId}: renames a workspace (see UpdateWorkspaceAsync).
        // Auth: authenticated user; super-admins bypass ownership checks in the handler.
        // Returns: 200 with the updated WorkspaceDto, 400/403/404 on failure.
        group.MapPut("/{workspaceId:guid}", UpdateWorkspaceAsync)
            .WithName("UpdateWorkspace")
            .Produces<WorkspaceDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        // DELETE /api/v1/workspaces/{workspaceId}: deletes a workspace (see DeleteWorkspaceAsync).
        // Auth: authenticated user; super-admins bypass ownership checks in the handler.
        // Returns: 200 with a boolean result, 403/404/409 on failure.
        group.MapDelete("/{workspaceId:guid}", DeleteWorkspaceAsync)
            .WithName("DeleteWorkspace")
            .Produces<bool>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        // POST /api/v1/workspaces/{workspaceId}/suspend: suspends a workspace (see SuspendWorkspaceAsync).
        // Auth: super-admin only (explicitly checked in the handler; returns 403 Forbid otherwise).
        // Returns: 200 with a boolean result, 403/404 on failure.
        group.MapPost("/{workspaceId:guid}/suspend", SuspendWorkspaceAsync)
            .WithName("SuspendWorkspace")
            .Produces<bool>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        // POST /api/v1/workspaces/{workspaceId}/reactivate: reactivates a suspended workspace (see ReactivateWorkspaceAsync).
        // Auth: super-admin only (explicitly checked in the handler; returns 403 Forbid otherwise).
        // Returns: 200 with a boolean result, 403/404 on failure.
        group.MapPost("/{workspaceId:guid}/reactivate", ReactivateWorkspaceAsync)
            .WithName("ReactivateWorkspace")
            .Produces<bool>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        // GET /api/v1/workspaces/{workspaceId}/members: lists a workspace's members.
        // Auth: any authenticated user (membership/authorization enforced by the handler).
        group.MapGet("/{workspaceId:guid}/members", async (
            Guid workspaceId,
            GetWorkspaceMembersHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new GetWorkspaceMembersQuery(workspaceId),
                cancellationToken)));
        // GET /api/v1/workspaces/{workspaceId}/projects: lists projects belonging to a workspace.
        // Auth: any authenticated user (membership/authorization enforced by the handler).
        group.MapGet("/{workspaceId:guid}/projects", async (
            Guid workspaceId,
            ListWorkspaceProjectsHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new ListWorkspaceProjectsQuery(workspaceId),
                cancellationToken)));
        // GET /api/v1/workspaces/{workspaceId}/activity: paged, optionally type-filtered activity feed for a workspace.
        // Auth: any authenticated user (membership/authorization enforced by the handler).
        // pageNumber/pageSize default to 1/20 when omitted (unbound value 0).
        group.MapGet("/{workspaceId:guid}/activity", async (
            Guid workspaceId,
            string? type,
            int pageNumber,
            int pageSize,
            GetWorkspaceActivityHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new GetWorkspaceActivityQuery(
                    workspaceId,
                    type,
                    pageNumber == 0 ? 1 : pageNumber,
                    pageSize == 0 ? 20 : pageSize),
                cancellationToken)));
        // POST /api/v1/workspaces/{workspaceId}/projects: creates a new project within a workspace.
        // Auth: any authenticated user (membership/authorization enforced by the handler).
        group.MapPost("/{workspaceId:guid}/projects", async (
            Guid workspaceId,
            CreateWorkspaceProjectRequest request,
            CreateWorkspaceProjectHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new CreateWorkspaceProjectCommand(
                    workspaceId,
                    request.Name,
                    request.Description,
                    request.TargetDate),
                cancellationToken)));
        // POST /api/v1/workspaces/{workspaceId}/members: directly adds an existing account as a member.
        // Auth: any authenticated user (role/authorization enforced by the handler).
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
        // PUT /api/v1/workspaces/{workspaceId}/members/{userId}: changes a member's workspace role.
        // Auth: any authenticated user (role/authorization enforced by the handler).
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
        // DELETE /api/v1/workspaces/{workspaceId}/members/{userId}: removes a member from a workspace.
        // Auth: any authenticated user (role/authorization enforced by the handler).
        group.MapDelete("/{workspaceId:guid}/members/{userId:guid}", async (
            Guid workspaceId,
            Guid userId,
            RemoveWorkspaceMemberHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new RemoveWorkspaceMemberCommand(workspaceId, userId),
                cancellationToken)));
        // GET /api/v1/workspaces/{workspaceId}/invitations: lists pending invitations for a workspace.
        // Auth: any authenticated user (role/authorization enforced by the handler).
        group.MapGet("/{workspaceId:guid}/invitations", async (
            Guid workspaceId,
            GetWorkspaceInvitationsHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new GetWorkspaceInvitationsQuery(workspaceId),
                cancellationToken)));
        // POST /api/v1/workspaces/{workspaceId}/invitations: invites a new member by email/name/role.
        // Auth: any authenticated user (role/authorization enforced by the handler).
        group.MapPost("/{workspaceId:guid}/invitations", async (
            Guid workspaceId,
            InviteWorkspaceMemberRequest request,
            InviteWorkspaceMemberHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new InviteWorkspaceMemberCommand(
                    workspaceId,
                    request.FullName,
                    request.Email,
                    request.Role),
                cancellationToken)));
        // DELETE /api/v1/workspaces/{workspaceId}/invitations/{invitationId}: cancels a pending invitation.
        // Auth: any authenticated user (role/authorization enforced by the handler).
        group.MapDelete("/{workspaceId:guid}/invitations/{invitationId:guid}", async (
            Guid workspaceId,
            Guid invitationId,
            CancelWorkspaceInvitationHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new CancelWorkspaceInvitationCommand(
                    workspaceId,
                    invitationId),
                cancellationToken)));

        // Public group (no RequireAuthorization): an invited person doesn't
        // have an account/session yet, so these are looked up by the
        // invitation's opaque token rather than by authenticated identity.
        var invitations = endpoints.MapGroup("/api/v1/invitations")
            .WithTags("Workspace Invitations");

        // GET /api/v1/invitations/{token}: looks up invitation details by token (e.g. to render an accept/decline page).
        // Auth: none (anonymous; token itself is the credential).
        invitations.MapGet("/{token}", async (
            string token,
            GetWorkspaceInvitationByTokenHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new GetWorkspaceInvitationByTokenQuery(token),
                cancellationToken)));
        // POST /api/v1/invitations/{token}/accept: accepts an invitation, creating/attaching the account as needed.
        // Auth: none (anonymous; token itself is the credential).
        invitations.MapPost("/{token}/accept", async (
            string token,
            AcceptWorkspaceInvitationRequest request,
            AcceptWorkspaceInvitationHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new AcceptWorkspaceInvitationCommand(
                    token,
                    request.DisplayName,
                    request.Password),
                cancellationToken)));
        // POST /api/v1/invitations/{token}/decline: declines a pending invitation.
        // Auth: none (anonymous; token itself is the credential).
        invitations.MapPost("/{token}/decline", async (
            string token,
            DeclineWorkspaceInvitationHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new DeclineWorkspaceInvitationCommand(token),
                cancellationToken)));

        return endpoints;
    }

    // Renames a workspace. Determines super-admin status first so the
    // command handler can allow the rename to bypass normal ownership checks.
    private static async Task<IResult> UpdateWorkspaceAsync(
        Guid workspaceId,
        UpdateWorkspaceRequest request,
        UpdateWorkspaceHandler handler,
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var isSuperAdmin = await SuperAdminAuthorization.IsSuperAdminAsync(
            currentUser,
            accounts,
            configuration,
            cancellationToken);

        return ApiResult.From(await handler.HandleAsync(
            new UpdateWorkspaceCommand(
                workspaceId,
                request.Name,
                isSuperAdmin),
            cancellationToken));
    }

    // Deletes a workspace. Determines super-admin status first so the
    // command handler can allow the deletion to bypass normal ownership checks.
    private static async Task<IResult> DeleteWorkspaceAsync(
        Guid workspaceId,
        DeleteWorkspaceHandler handler,
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var isSuperAdmin = await SuperAdminAuthorization.IsSuperAdminAsync(
            currentUser,
            accounts,
            configuration,
            cancellationToken);

        return ApiResult.From(await handler.HandleAsync(
            new DeleteWorkspaceCommand(workspaceId, isSuperAdmin),
            cancellationToken));
    }

    // Suspends a workspace. Restricted to super-admins; returns 403 Forbid
    // immediately for any other caller before invoking the command handler.
    private static async Task<IResult> SuspendWorkspaceAsync(
        Guid workspaceId,
        SuspendWorkspaceRequest request,
        SuspendWorkspaceHandler handler,
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
            new SuspendWorkspaceCommand(workspaceId, request.Reason),
            cancellationToken));
    }

    // Reactivates a suspended workspace. Restricted to super-admins; returns
    // 403 Forbid immediately for any other caller before invoking the command handler.
    private static async Task<IResult> ReactivateWorkspaceAsync(
        Guid workspaceId,
        ReactivateWorkspaceHandler handler,
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
            new ReactivateWorkspaceCommand(workspaceId),
            cancellationToken));
    }
}

/// <summary>Request body for creating a project within a workspace.</summary>
public sealed record CreateWorkspaceProjectRequest(
    string Name,
    string? Description = null,
    DateOnly? TargetDate = null);

/// <summary>Request body for creating a new workspace.</summary>
public sealed record CreateWorkspaceRequest(string Name);

/// <summary>Request body for renaming a workspace.</summary>
public sealed record UpdateWorkspaceRequest(string Name);

/// <summary>Request body for suspending a workspace, with an optional reason.</summary>
public sealed record SuspendWorkspaceRequest(string? Reason);

/// <summary>Request body for inviting a new member to a workspace by email.</summary>
public sealed record InviteWorkspaceMemberRequest(
    string FullName,
    string Email,
    TodoApp.Domain.Collaboration.WorkspaceRole Role);

/// <summary>Request body for accepting a workspace invitation, optionally setting up a new account.</summary>
public sealed record AcceptWorkspaceInvitationRequest(
    string? DisplayName,
    string? Password);
