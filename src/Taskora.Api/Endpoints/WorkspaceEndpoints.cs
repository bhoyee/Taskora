using TodoApp.Api.Contracts;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Collaboration;
using TodoApp.Application.Projects;
using TodoApp.Application.Tasks.Activity;

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
        group.MapPost("/", async (
            CreateWorkspaceRequest request,
            CreateWorkspaceHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new CreateWorkspaceCommand(request.Name),
                cancellationToken)));
        group.MapPut("/{workspaceId:guid}", UpdateWorkspaceAsync)
            .WithName("UpdateWorkspace")
            .Produces<WorkspaceDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapDelete("/{workspaceId:guid}", DeleteWorkspaceAsync)
            .WithName("DeleteWorkspace")
            .Produces<bool>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapGet("/{workspaceId:guid}/members", async (
            Guid workspaceId,
            GetWorkspaceMembersHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new GetWorkspaceMembersQuery(workspaceId),
                cancellationToken)));
        group.MapGet("/{workspaceId:guid}/projects", async (
            Guid workspaceId,
            ListWorkspaceProjectsHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new ListWorkspaceProjectsQuery(workspaceId),
                cancellationToken)));
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
        group.MapGet("/{workspaceId:guid}/invitations", async (
            Guid workspaceId,
            GetWorkspaceInvitationsHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new GetWorkspaceInvitationsQuery(workspaceId),
                cancellationToken)));
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

        var invitations = endpoints.MapGroup("/api/v1/invitations")
            .WithTags("Workspace Invitations");

        invitations.MapGet("/{token}", async (
            string token,
            GetWorkspaceInvitationByTokenHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new GetWorkspaceInvitationByTokenQuery(token),
                cancellationToken)));
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
        invitations.MapPost("/{token}/decline", async (
            string token,
            DeclineWorkspaceInvitationHandler handler,
            CancellationToken cancellationToken) =>
            ApiResult.From(await handler.HandleAsync(
                new DeclineWorkspaceInvitationCommand(token),
                cancellationToken)));

        return endpoints;
    }

    private static async Task<IResult> UpdateWorkspaceAsync(
        Guid workspaceId,
        UpdateWorkspaceRequest request,
        UpdateWorkspaceHandler handler,
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var isSuperAdmin = await IsSuperAdminAsync(
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

    private static async Task<IResult> DeleteWorkspaceAsync(
        Guid workspaceId,
        DeleteWorkspaceHandler handler,
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var isSuperAdmin = await IsSuperAdminAsync(
            currentUser,
            accounts,
            configuration,
            cancellationToken);

        return ApiResult.From(await handler.HandleAsync(
            new DeleteWorkspaceCommand(workspaceId, isSuperAdmin),
            cancellationToken));
    }

    private static async Task<bool> IsSuperAdminAsync(
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(
            currentUser.UserId,
            cancellationToken);
        if (account is null)
        {
            return false;
        }

        var emails = configuration
            .GetSection("Administration:SuperAdminEmails")
            .Get<string[]>() ?? [];
        var singleEmail = configuration["Administration:SuperAdminEmail"];
        if (!string.IsNullOrWhiteSpace(singleEmail))
        {
            emails = [.. emails, singleEmail];
        }

        return emails.Any(candidate =>
            account.User.Email.Equals(
                candidate?.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record CreateWorkspaceProjectRequest(
    string Name,
    string? Description = null,
    DateOnly? TargetDate = null);

public sealed record CreateWorkspaceRequest(string Name);

public sealed record UpdateWorkspaceRequest(string Name);

public sealed record InviteWorkspaceMemberRequest(
    string FullName,
    string Email,
    TodoApp.Domain.Collaboration.WorkspaceRole Role);

public sealed record AcceptWorkspaceInvitationRequest(
    string? DisplayName,
    string? Password);
