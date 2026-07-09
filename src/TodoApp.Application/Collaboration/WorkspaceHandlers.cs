using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Common;
using TodoApp.Domain.Projects;

namespace TodoApp.Application.Collaboration;

public sealed class GetMyWorkspacesHandler(
    IWorkspaceRepository workspaces,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<WorkspaceDto>>> HandleAsync(
        GetMyWorkspacesQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Unauthorized<IReadOnlyList<WorkspaceDto>>();
        }

        var result = await workspaces.ListForUserAsync(
            currentUser.UserId,
            cancellationToken);
        return Result<IReadOnlyList<WorkspaceDto>>.Success(
            result.Select(workspace => new WorkspaceDto(
                workspace.Id,
                workspace.Name,
                workspace.Memberships
                    .Single(member => member.UserId == currentUser.UserId)
                    .Role))
                .ToArray());
    }

    internal static Result<T> Unauthorized<T>() =>
        Result<T>.Failure(new ApplicationError(
            "identity.unauthorized",
            "Authentication is required.",
            ErrorType.Unauthorized));
}

public sealed class CreateWorkspaceHandler(
    IWorkspaceRepository workspaces,
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    ICurrentUser currentUser)
{
    public async Task<Result<WorkspaceDto>> HandleAsync(
        CreateWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return GetMyWorkspacesHandler.Unauthorized<WorkspaceDto>();
        }

        try
        {
            var workspace = Workspace.Create(
                identifiers.NewId(),
                command.Name,
                currentUser.UserId);
            var project = Project.Create(
                identifiers.NewId(),
                "My task project",
                "Starter project created for this workspace.",
                workspace.Id);

            await workspaces.AddAsync(workspace, cancellationToken);
            await projects.AddAsync(project, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<WorkspaceDto>.Success(
                new WorkspaceDto(
                    workspace.Id,
                    workspace.Name,
                    WorkspaceRole.Owner));
        }
        catch (DomainValidationException exception)
        {
            return Result<WorkspaceDto>.Failure(
                new ApplicationError(
                    "workspace.validation",
                    exception.Message,
                    ErrorType.Validation));
        }
    }
}

public sealed class GetWorkspaceMembersHandler(
    IWorkspaceRepository workspaces,
    IUserProfileRepository profiles,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<WorkspaceMemberDto>>> HandleAsync(
        GetWorkspaceMembersQuery query,
        CancellationToken cancellationToken)
    {
        var access = await GetWorkspaceAsync(
            workspaces, currentUser, query.WorkspaceId, cancellationToken);
        if (!access.IsSuccess)
        {
            return Result<IReadOnlyList<WorkspaceMemberDto>>.Failure(access.Error);
        }

        var users = await profiles.GetByIdsAsync(
            access.Value.Memberships.Select(member => member.UserId).ToArray(),
            cancellationToken);
        return Result<IReadOnlyList<WorkspaceMemberDto>>.Success(
            access.Value.Memberships.Select(membership =>
            {
                var user = users.Single(item => item.Id == membership.UserId);
                return new WorkspaceMemberDto(
                    user.Id,
                    user.DisplayName,
                    user.Email,
                    membership.Role);
            }).ToArray());
    }

    internal static async Task<Result<Workspace>> GetWorkspaceAsync(
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return GetMyWorkspacesHandler.Unauthorized<Workspace>();
        }

        var workspace = await workspaces.GetByIdAsync(
            workspaceId, cancellationToken);
        if (workspace is null || !workspace.HasMember(currentUser.UserId))
        {
            return Result<Workspace>.Failure(new ApplicationError(
                "workspace.not_found",
                "The workspace was not found.",
                ErrorType.NotFound));
        }

        return Result<Workspace>.Success(workspace);
    }
}

public sealed class AddWorkspaceMemberHandler(
    IWorkspaceRepository workspaces,
    IUserProfileRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(
        AddWorkspaceMemberCommand command,
        CancellationToken cancellationToken)
    {
        var access = await GetWorkspaceMembersHandler.GetWorkspaceAsync(
            workspaces, currentUser, command.WorkspaceId, cancellationToken);
        if (!access.IsSuccess) return Result<bool>.Failure(access.Error);
        var user = await users.GetByEmailAsync(
            command.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null)
        {
            return Result<bool>.Failure(new ApplicationError(
                "user.not_found", "The user was not found.", ErrorType.NotFound));
        }

        try
        {
            access.Value.AddMember(currentUser.UserId, user.Id, command.Role);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (DomainRuleException exception)
        {
            return Result<bool>.Failure(new ApplicationError(
                "workspace.forbidden", exception.Message, ErrorType.Forbidden));
        }
    }
}

public sealed class ChangeWorkspaceRoleHandler(
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(
        ChangeWorkspaceRoleCommand command,
        CancellationToken cancellationToken)
    {
        var access = await GetWorkspaceMembersHandler.GetWorkspaceAsync(
            workspaces, currentUser, command.WorkspaceId, cancellationToken);
        if (!access.IsSuccess) return Result<bool>.Failure(access.Error);

        try
        {
            access.Value.ChangeRole(
                currentUser.UserId, command.UserId, command.Role);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (DomainRuleException exception)
        {
            return Result<bool>.Failure(new ApplicationError(
                "workspace.forbidden", exception.Message, ErrorType.Forbidden));
        }
    }
}

public sealed class RemoveWorkspaceMemberHandler(
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(
        RemoveWorkspaceMemberCommand command,
        CancellationToken cancellationToken)
    {
        var access = await GetWorkspaceMembersHandler.GetWorkspaceAsync(
            workspaces, currentUser, command.WorkspaceId, cancellationToken);
        if (!access.IsSuccess) return Result<bool>.Failure(access.Error);

        try
        {
            access.Value.RemoveMember(currentUser.UserId, command.UserId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (DomainRuleException exception)
        {
            return Result<bool>.Failure(new ApplicationError(
                "workspace.forbidden", exception.Message, ErrorType.Forbidden));
        }
    }
}
