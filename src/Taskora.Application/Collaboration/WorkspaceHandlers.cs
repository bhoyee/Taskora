using TodoApp.Application.Abstractions;
using TodoApp.Application.Accounts;
using TodoApp.Application.Common;
using TodoApp.Application.Notifications;
using TodoApp.Application.PublicDemo;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Common;
using TodoApp.Domain.Projects;

namespace TodoApp.Application.Collaboration;

/// <summary>Lists the workspaces the current user belongs to, along with their role in each.</summary>
public sealed class GetMyWorkspacesHandler(
    IWorkspaceRepository workspaces,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication, then returns every workspace the current
    /// user is a member of, each paired with the caller's role in that
    /// workspace.
    /// </summary>
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

    // Shared "authentication required" failure reused by other workspace handlers.
    internal static Result<T> Unauthorized<T>() =>
        Result<T>.Failure(new ApplicationError(
            "identity.unauthorized",
            "Authentication is required.",
            ErrorType.Unauthorized));
}

/// <summary>Creates a new workspace owned by the current user.</summary>
public sealed class CreateWorkspaceHandler(
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication, then creates a new workspace owned by the
    /// current user and persists it, translating domain validation failures
    /// into a validation error.
    /// </summary>
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

            await workspaces.AddAsync(workspace, cancellationToken);
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

/// <summary>Renames a workspace.</summary>
public sealed class UpdateWorkspaceHandler(
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and, unless
    /// <see cref="UpdateWorkspaceCommand.HasAdministrativeBypass"/> is set,
    /// requires the current user to be an active member of a non-suspended
    /// workspace. Renames the workspace (as the owner when using the
    /// administrative bypass, or as the current user otherwise, letting
    /// domain rules enforce who may rename), translating domain
    /// validation/rule violations into typed failures.
    /// </summary>
    public async Task<Result<WorkspaceDto>> HandleAsync(
        UpdateWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return GetMyWorkspacesHandler.Unauthorized<WorkspaceDto>();
        }

        var workspace = await workspaces.GetByIdAsync(
            command.WorkspaceId,
            cancellationToken);
        if (workspace is null ||
            (!command.HasAdministrativeBypass &&
             (!workspace.HasMember(currentUser.UserId) ||
              workspace.IsSuspended)))
        {
            return WorkspaceHandlerErrors.WorkspaceNotFound<WorkspaceDto>();
        }

        try
        {
            if (command.HasAdministrativeBypass)
            {
                workspace.Rename(workspace.OwnerId, command.Name);
            }
            else
            {
                workspace.Rename(currentUser.UserId, command.Name);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            var role = workspace.HasMember(currentUser.UserId)
                ? workspace.GetRole(currentUser.UserId)
                : WorkspaceRole.Owner;
            return Result<WorkspaceDto>.Success(
                new WorkspaceDto(workspace.Id, workspace.Name, role));
        }
        catch (DomainValidationException exception)
        {
            return Result<WorkspaceDto>.Failure(
                new ApplicationError(
                    "workspace.validation",
                    exception.Message,
                    ErrorType.Validation));
        }
        catch (DomainRuleException exception)
        {
            return Result<WorkspaceDto>.Failure(
                new ApplicationError(
                    "workspace.forbidden",
                    exception.Message,
                    ErrorType.Forbidden));
        }
    }
}

/// <summary>Deletes a workspace.</summary>
public sealed class DeleteWorkspaceHandler(
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and, unless
    /// <see cref="DeleteWorkspaceCommand.HasAdministrativeBypass"/> is set,
    /// requires active membership in a non-suspended workspace, that the
    /// user belongs to more than one workspace (a user's last remaining
    /// workspace cannot be deleted), and that the user holds the Owner role.
    /// Removes the workspace and its data on success.
    /// </summary>
    public async Task<Result<bool>> HandleAsync(
        DeleteWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return GetMyWorkspacesHandler.Unauthorized<bool>();
        }

        var workspace = await workspaces.GetByIdAsync(
            command.WorkspaceId,
            cancellationToken);
        if (workspace is null ||
            (!command.HasAdministrativeBypass &&
             (!workspace.HasMember(currentUser.UserId) ||
              workspace.IsSuspended)))
        {
            return WorkspaceHandlerErrors.WorkspaceNotFound<bool>();
        }

        if (command.HasAdministrativeBypass &&
            !PublicDemoIdentifiers.AllowsDestructiveBypass(currentUser.UserId, workspace.Id))
        {
            return WorkspaceHandlerErrors.DemoAdministrativeActionRestricted<bool>();
        }

        if (!command.HasAdministrativeBypass)
        {
            var userWorkspaces = await workspaces.ListForUserAsync(
                currentUser.UserId,
                cancellationToken);
            if (userWorkspaces.Count <= 1)
            {
                return Result<bool>.Failure(
                    new ApplicationError(
                        "workspace.last_workspace",
                        "Create or switch to another workspace before deleting this one.",
                        ErrorType.Conflict));
            }
        }

        if (!command.HasAdministrativeBypass &&
            workspace.GetRole(currentUser.UserId) != WorkspaceRole.Owner)
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "workspace.forbidden",
                    "Only the workspace owner can delete this workspace.",
                    ErrorType.Forbidden));
        }

        await workspaces.RemoveAsync(workspace, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

/// <summary>Suspends a workspace, blocking its members from further activity, for administrative/moderation use.</summary>
public sealed class SuspendWorkspaceHandler(
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the workspace to exist, then suspends it with the given
    /// reason, recording the acting user and timestamp. This handler is
    /// intended for administrative/platform callers and does not itself
    /// check workspace membership.
    /// </summary>
    public async Task<Result<bool>> HandleAsync(
        SuspendWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaces.GetByIdAsync(
            command.WorkspaceId,
            cancellationToken);
        if (workspace is null)
        {
            return WorkspaceHandlerErrors.WorkspaceNotFound<bool>();
        }

        if (!PublicDemoIdentifiers.AllowsDestructiveBypass(currentUser.UserId, workspace.Id))
        {
            return WorkspaceHandlerErrors.DemoAdministrativeActionRestricted<bool>();
        }

        workspace.Suspend(currentUser.UserId, command.Reason, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

/// <summary>Lifts a suspension on a workspace, for administrative/moderation use.</summary>
public sealed class ReactivateWorkspaceHandler(
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the workspace to exist, then reactivates it, clearing its
    /// suspended state.
    /// </summary>
    public async Task<Result<bool>> HandleAsync(
        ReactivateWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaces.GetByIdAsync(
            command.WorkspaceId,
            cancellationToken);
        if (workspace is null)
        {
            return WorkspaceHandlerErrors.WorkspaceNotFound<bool>();
        }

        if (!PublicDemoIdentifiers.AllowsDestructiveBypass(currentUser.UserId, workspace.Id))
        {
            return WorkspaceHandlerErrors.DemoAdministrativeActionRestricted<bool>();
        }

        workspace.Reactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

/// <summary>Lists the members of a workspace with their display names, emails, and roles.</summary>
public sealed class GetWorkspaceMembersHandler(
    IWorkspaceRepository workspaces,
    IUserProfileRepository profiles,
    ICurrentUser currentUser)
{
    /// <summary>
    /// When <see cref="GetWorkspaceMembersQuery.HasAdministrativeBypass"/> is
    /// set, requires only that the workspace exist (for administrative/
    /// platform callers); otherwise requires the current user to be an
    /// active member of a non-suspended workspace. Returns each member's
    /// profile joined with their workspace role.
    /// </summary>
    public async Task<Result<IReadOnlyList<WorkspaceMemberDto>>> HandleAsync(
        GetWorkspaceMembersQuery query,
        CancellationToken cancellationToken)
    {
        Workspace workspace;
        if (query.HasAdministrativeBypass)
        {
            var found = await workspaces.GetByIdAsync(
                query.WorkspaceId, cancellationToken);
            if (found is null)
            {
                return Result<IReadOnlyList<WorkspaceMemberDto>>.Failure(
                    new ApplicationError(
                        "workspace.not_found",
                        "The workspace was not found.",
                        ErrorType.NotFound));
            }

            workspace = found;
        }
        else
        {
            var access = await GetWorkspaceAsync(
                workspaces, currentUser, query.WorkspaceId, cancellationToken);
            if (!access.IsSuccess)
            {
                return Result<IReadOnlyList<WorkspaceMemberDto>>.Failure(access.Error);
            }

            workspace = access.Value;
        }

        var users = await profiles.GetByIdsAsync(
            workspace.Memberships.Select(member => member.UserId).ToArray(),
            cancellationToken);
        return Result<IReadOnlyList<WorkspaceMemberDto>>.Success(
            workspace.Memberships.Select(membership =>
            {
                var user = users.Single(item => item.Id == membership.UserId);
                return new WorkspaceMemberDto(
                    user.Id,
                    user.DisplayName,
                    user.Email,
                    membership.Role);
            }).ToArray());
    }

    /// <summary>
    /// Shared workspace-access check reused across workspace/project
    /// handlers: requires authentication, that the workspace exist, that the
    /// current user is a member of it, and that the workspace is not
    /// suspended. Returns the workspace on success.
    /// </summary>
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
        if (workspace is null ||
            !workspace.HasMember(currentUser.UserId) ||
            workspace.IsSuspended)
        {
            return Result<Workspace>.Failure(new ApplicationError(
                "workspace.not_found",
                "The workspace was not found.",
                ErrorType.NotFound));
        }

        return Result<Workspace>.Success(workspace);
    }
}

/// <summary>Shared error-construction helper local to this file.</summary>
file static class WorkspaceHandlerErrors
{
    // Builds the "workspace not found" failure.
    public static Result<T> WorkspaceNotFound<T>() =>
        Result<T>.Failure(new ApplicationError(
            "workspace.not_found",
            "The workspace was not found.",
            ErrorType.NotFound));

    // Builds the failure returned when the public demo's Super Admin persona
    // attempts a destructive administrative action against a workspace other
    // than the demo's own.
    public static Result<T> DemoAdministrativeActionRestricted<T>() =>
        Result<T>.Failure(new ApplicationError(
            "workspace.demo_restricted",
            "The public demo's Super Admin account can't delete or suspend other workspaces.",
            ErrorType.Forbidden));
}

/// <summary>Adds an existing user directly to a workspace by email, without going through an invitation.</summary>
public sealed class AddWorkspaceMemberHandler(
    IWorkspaceRepository workspaces,
    IUserProfileRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the current user to have access to the workspace, requires a
    /// user account to exist for the given email, then adds that user as a
    /// member with the requested role, letting the domain enforce who may
    /// add members (surfaced as a Forbidden failure on violation).
    /// </summary>
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

/// <summary>Changes a workspace member's role.</summary>
public sealed class ChangeWorkspaceRoleHandler(
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the current user to have access to the workspace, then
    /// applies the role change, letting the domain enforce who may change
    /// roles (surfaced as a Forbidden failure on violation).
    /// </summary>
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

/// <summary>Removes a member from a workspace.</summary>
public sealed class RemoveWorkspaceMemberHandler(
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the current user to have access to the workspace, then
    /// removes the target member, letting the domain enforce who may be
    /// removed and by whom (surfaced as a Forbidden failure on violation).
    /// </summary>
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

/// <summary>Invites a (possibly new) user to join a workspace by email.</summary>
public sealed class InviteWorkspaceMemberHandler(
    IWorkspaceRepository workspaces,
    IWorkspaceInvitationRepository invitations,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    ICurrentUser currentUser,
    IBackgroundEmailDispatcher emailDispatcher,
    IApplicationLinkBuilder links)
{
    /// <summary>
    /// Requires the current user to hold the Owner role in the workspace
    /// (via <see cref="RequireOwnerAsync"/>), then creates a pending
    /// invitation valid for 7 days, persists it, and dispatches the invite
    /// link to the invitee in the background so a slow SMTP round-trip can't
    /// hang this request.
    /// </summary>
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
            emailDispatcher.Dispatch(
                BuildInvitationMessage(invitation, access.Value.Name));

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

    /// <summary>
    /// Shared authorization check for invitation-management operations:
    /// requires workspace access (see
    /// <see cref="GetWorkspaceMembersHandler.GetWorkspaceAsync"/>) and that
    /// the current user's role is Owner, returning a Forbidden failure
    /// otherwise.
    /// </summary>
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

    // Maps an invitation entity to its DTO, optionally including the shareable invite link.
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

    // Builds a validation-typed failure result, shared by invitation handlers.
    internal static Result<WorkspaceInvitationDto> ValidationFailure(
        string description) =>
        Result<WorkspaceInvitationDto>.Failure(
            new ApplicationError(
                "invitation.validation",
                description,
                ErrorType.Validation));

    // Builds a conflict-typed failure result, shared by invitation handlers.
    internal static Result<WorkspaceInvitationDto> ConflictFailure(
        string description) =>
        Result<WorkspaceInvitationDto>.Failure(
            new ApplicationError(
                "invitation.conflict",
                description,
                ErrorType.Conflict));

    // Composes the workspace invitation email, including the accept/decline link.
    private NotificationEmailMessage BuildInvitationMessage(
        WorkspaceInvitation invitation,
        string workspaceName)
    {
        var inviteLink = links.BuildInvitationLink(invitation.Token);
        return TaskoraEmailTemplate.Build(
            [invitation.Email],
            $"Workspace invitation: Join {workspaceName}",
            "Workspace invitation",
            $"Join {workspaceName} on Taskora",
            $"Hello {invitation.FullName},",
            $"You have been invited to join the {workspaceName} workspace in Taskora.",
            [
                new EmailDetail("Workspace", workspaceName),
                new EmailDetail("Role", invitation.Role.ToString()),
                new EmailDetail("Invitation expires", invitation.ExpiresAt.ToString("yyyy-MM-dd"))
            ],
            "Accept or decline invitation",
            inviteLink,
            "If you were not expecting this invitation, you can ignore this message.");
    }
}

/// <summary>Lists all invitations issued for a workspace.</summary>
public sealed class GetWorkspaceInvitationsHandler(
    IWorkspaceRepository workspaces,
    IWorkspaceInvitationRepository invitations,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the current user to hold the Owner role in the workspace,
    /// then returns all invitations for it, including the shareable invite
    /// link only for invitations still in the Pending state.
    /// </summary>
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

/// <summary>Looks up a workspace invitation by its token, for use on the public invite-acceptance page.</summary>
public sealed class GetWorkspaceInvitationByTokenHandler(
    IWorkspaceInvitationRepository invitations,
    IWorkspaceRepository workspaces)
{
    /// <summary>
    /// Resolves the invitation and its workspace by token; this is an
    /// unauthenticated lookup (used before the invitee has an account),
    /// returning <see cref="ErrorType.NotFound"/> if either is missing.
    /// </summary>
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

    // Builds the "invitation not found" failure, shared by invitation handlers.
    internal static Result<WorkspaceInvitationDto> NotFound() =>
        Result<WorkspaceInvitationDto>.Failure(
            new ApplicationError(
                "invitation.not_found",
                "The workspace invitation was not found.",
                ErrorType.NotFound));
}

/// <summary>Accepts a workspace invitation, creating the invitee's account first if needed.</summary>
public sealed class AcceptWorkspaceInvitationHandler(
    IWorkspaceInvitationRepository invitations,
    IWorkspaceRepository workspaces,
    IUserProfileRepository users,
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock)
{
    /// <summary>
    /// Resolves the invitation and workspace by token (not-found failure if
    /// either is missing). If no account exists yet for the invited email,
    /// requires a valid new password and creates the account; otherwise uses
    /// the existing account. Adds the user to the workspace with the
    /// invited role and marks the invitation accepted, translating domain
    /// validation/rule violations into typed failures.
    /// </summary>
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

/// <summary>Declines a workspace invitation.</summary>
public sealed class DeclineWorkspaceInvitationHandler(
    IWorkspaceInvitationRepository invitations,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>
    /// Resolves the invitation and workspace by token (not-found failure if
    /// either is missing), then marks the invitation declined, translating a
    /// domain rule violation (e.g. already resolved) into a conflict
    /// failure.
    /// </summary>
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

/// <summary>Cancels a pending workspace invitation before it is accepted or declined.</summary>
public sealed class CancelWorkspaceInvitationHandler(
    IWorkspaceInvitationRepository invitations,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the current user to hold the Owner role in the workspace,
    /// requires the invitation to exist and belong to that workspace, then
    /// cancels it, translating a domain rule violation (e.g. already
    /// resolved) into a conflict failure.
    /// </summary>
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
