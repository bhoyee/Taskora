using TodoApp.Application.Abstractions;
using TodoApp.Application.Accounts;
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

public sealed class InviteWorkspaceMemberHandler(
    IWorkspaceRepository workspaces,
    IWorkspaceInvitationRepository invitations,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<WorkspaceInvitationDto>> HandleAsync(
        InviteWorkspaceMemberCommand command,
        CancellationToken cancellationToken)
    {
        var access = await RequireOwnerAsync(
            workspaces,
            currentUser,
            command.WorkspaceId,
            cancellationToken);
        if (!access.IsSuccess)
        {
            return Result<WorkspaceInvitationDto>.Failure(access.Error);
        }

        try
        {
            var now = clock.UtcNow;
            var invitation = WorkspaceInvitation.Create(
                identifiers.NewId(),
                command.WorkspaceId,
                command.FullName,
                command.Email,
                command.Role,
                currentUser.UserId,
                identifiers.NewId().ToString("N"),
                now,
                now.AddDays(7));

            await invitations.AddAsync(invitation, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<WorkspaceInvitationDto>.Success(
                ToInvitationDto(invitation, access.Value.Name, includeLink: true));
        }
        catch (DomainValidationException exception)
        {
            return ValidationFailure(exception.Message);
        }
        catch (DomainRuleException exception)
        {
            return ConflictFailure(exception.Message);
        }
    }

    internal static async Task<Result<Workspace>> RequireOwnerAsync(
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var access = await GetWorkspaceMembersHandler.GetWorkspaceAsync(
            workspaces,
            currentUser,
            workspaceId,
            cancellationToken);
        if (!access.IsSuccess)
        {
            return access;
        }

        if (access.Value.GetRole(currentUser.UserId) != WorkspaceRole.Owner)
        {
            return Result<Workspace>.Failure(
                new ApplicationError(
                    "workspace.forbidden",
                    "Only the workspace owner can perform this action.",
                    ErrorType.Forbidden));
        }

        return access;
    }

    internal static WorkspaceInvitationDto ToInvitationDto(
        WorkspaceInvitation invitation,
        string workspaceName,
        bool includeLink = false) =>
        new(
            invitation.Id,
            invitation.WorkspaceId,
            workspaceName,
            invitation.FullName,
            invitation.Email,
            invitation.Role,
            invitation.Status,
            invitation.CreatedAt,
            invitation.ExpiresAt,
            includeLink ? $"/invite/{invitation.Token}" : null);

    internal static Result<WorkspaceInvitationDto> ValidationFailure(
        string description) =>
        Result<WorkspaceInvitationDto>.Failure(
            new ApplicationError(
                "invitation.validation",
                description,
                ErrorType.Validation));

    internal static Result<WorkspaceInvitationDto> ConflictFailure(
        string description) =>
        Result<WorkspaceInvitationDto>.Failure(
            new ApplicationError(
                "invitation.conflict",
                description,
                ErrorType.Conflict));
}

public sealed class GetWorkspaceInvitationsHandler(
    IWorkspaceRepository workspaces,
    IWorkspaceInvitationRepository invitations,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<WorkspaceInvitationDto>>> HandleAsync(
        GetWorkspaceInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await InviteWorkspaceMemberHandler.RequireOwnerAsync(
            workspaces,
            currentUser,
            query.WorkspaceId,
            cancellationToken);
        if (!access.IsSuccess)
        {
            return Result<IReadOnlyList<WorkspaceInvitationDto>>.Failure(
                access.Error);
        }

        var result = await invitations.ListForWorkspaceAsync(
            query.WorkspaceId,
            cancellationToken);
        return Result<IReadOnlyList<WorkspaceInvitationDto>>.Success(
            result.Select(invitation =>
                InviteWorkspaceMemberHandler.ToInvitationDto(
                    invitation,
                    access.Value.Name,
                    includeLink: invitation.Status ==
                        WorkspaceInvitationStatus.Pending))
                .ToArray());
    }
}

public sealed class GetWorkspaceInvitationByTokenHandler(
    IWorkspaceInvitationRepository invitations,
    IWorkspaceRepository workspaces)
{
    public async Task<Result<WorkspaceInvitationDto>> HandleAsync(
        GetWorkspaceInvitationByTokenQuery query,
        CancellationToken cancellationToken)
    {
        var invitation = await invitations.GetByTokenAsync(
            query.Token,
            cancellationToken);
        if (invitation is null)
        {
            return NotFound();
        }

        var workspace = await workspaces.GetByIdAsync(
            invitation.WorkspaceId,
            cancellationToken);
        if (workspace is null)
        {
            return NotFound();
        }

        return Result<WorkspaceInvitationDto>.Success(
            InviteWorkspaceMemberHandler.ToInvitationDto(
                invitation,
                workspace.Name));
    }

    internal static Result<WorkspaceInvitationDto> NotFound() =>
        Result<WorkspaceInvitationDto>.Failure(
            new ApplicationError(
                "invitation.not_found",
                "The workspace invitation was not found.",
                ErrorType.NotFound));
}

public sealed class AcceptWorkspaceInvitationHandler(
    IWorkspaceInvitationRepository invitations,
    IWorkspaceRepository workspaces,
    IUserProfileRepository users,
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock)
{
    public async Task<Result<WorkspaceInvitationDto>> HandleAsync(
        AcceptWorkspaceInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var invitation = await invitations.GetByTokenAsync(
            command.Token,
            cancellationToken);
        if (invitation is null)
        {
            return GetWorkspaceInvitationByTokenHandler.NotFound();
        }

        var workspace = await workspaces.GetByIdAsync(
            invitation.WorkspaceId,
            cancellationToken);
        if (workspace is null)
        {
            return GetWorkspaceInvitationByTokenHandler.NotFound();
        }

        try
        {
            var user = await users.GetByEmailAsync(
                invitation.Email,
                cancellationToken);
            if (user is null)
            {
                if (string.IsNullOrWhiteSpace(command.Password) ||
                    command.Password.Length < 8)
                {
                    return InviteWorkspaceMemberHandler.ValidationFailure(
                        "Password must be at least 8 characters.");
                }

                user = UserProfile.Create(
                    identifiers.NewId(),
                    string.IsNullOrWhiteSpace(command.DisplayName)
                        ? invitation.FullName
                        : command.DisplayName,
                    invitation.Email);
                await accounts.AddUserAsync(
                    user,
                    PasswordHasher.Hash(command.Password),
                    cancellationToken);
            }

            workspace.AddMember(
                workspace.OwnerId,
                user.Id,
                invitation.Role);
            invitation.Accept(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<WorkspaceInvitationDto>.Success(
                InviteWorkspaceMemberHandler.ToInvitationDto(
                    invitation,
                    workspace.Name));
        }
        catch (DomainValidationException exception)
        {
            return InviteWorkspaceMemberHandler.ValidationFailure(
                exception.Message);
        }
        catch (DomainRuleException exception)
        {
            return InviteWorkspaceMemberHandler.ConflictFailure(
                exception.Message);
        }
    }
}

public sealed class DeclineWorkspaceInvitationHandler(
    IWorkspaceInvitationRepository invitations,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<WorkspaceInvitationDto>> HandleAsync(
        DeclineWorkspaceInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var invitation = await invitations.GetByTokenAsync(
            command.Token,
            cancellationToken);
        if (invitation is null)
        {
            return GetWorkspaceInvitationByTokenHandler.NotFound();
        }

        var workspace = await workspaces.GetByIdAsync(
            invitation.WorkspaceId,
            cancellationToken);
        if (workspace is null)
        {
            return GetWorkspaceInvitationByTokenHandler.NotFound();
        }

        try
        {
            invitation.Decline(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<WorkspaceInvitationDto>.Success(
                InviteWorkspaceMemberHandler.ToInvitationDto(
                    invitation,
                    workspace.Name));
        }
        catch (DomainRuleException exception)
        {
            return InviteWorkspaceMemberHandler.ConflictFailure(
                exception.Message);
        }
    }
}

public sealed class CancelWorkspaceInvitationHandler(
    IWorkspaceInvitationRepository invitations,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<WorkspaceInvitationDto>> HandleAsync(
        CancelWorkspaceInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var access = await InviteWorkspaceMemberHandler.RequireOwnerAsync(
            workspaces,
            currentUser,
            command.WorkspaceId,
            cancellationToken);
        if (!access.IsSuccess)
        {
            return Result<WorkspaceInvitationDto>.Failure(access.Error);
        }

        var invitation = await invitations.GetByIdAsync(
            command.InvitationId,
            cancellationToken);
        if (invitation is null ||
            invitation.WorkspaceId != command.WorkspaceId)
        {
            return GetWorkspaceInvitationByTokenHandler.NotFound();
        }

        try
        {
            invitation.Cancel(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<WorkspaceInvitationDto>.Success(
                InviteWorkspaceMemberHandler.ToInvitationDto(
                    invitation,
                    access.Value.Name));
        }
        catch (DomainRuleException exception)
        {
            return InviteWorkspaceMemberHandler.ConflictFailure(
                exception.Message);
        }
    }
}
