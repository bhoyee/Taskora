using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class WorkspaceRepository(TodoAppDbContext context)
    : IWorkspaceRepository
{
    public async Task AddAsync(
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        await context.Workspaces.AddAsync(workspace, cancellationToken);
    }

    public Task<Workspace?> GetByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken) =>
        context.Workspaces
            .Include("_memberships")
            .SingleOrDefaultAsync(
                workspace => workspace.Id == workspaceId,
                cancellationToken);

    public async Task<IReadOnlyList<Workspace>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await context.Workspaces
            .AsNoTracking()
            .Include("_memberships")
            .Where(workspace =>
                context.WorkspaceMemberships.Any(membership =>
                    membership.WorkspaceId == workspace.Id &&
                    membership.UserId == userId))
            .OrderBy(workspace => workspace.Name)
            .ToArrayAsync(cancellationToken);
}

public sealed class UserProfileRepository(TodoAppDbContext context)
    : IUserProfileRepository
{
    public Task<UserProfile?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken) =>
        context.UserProfiles.SingleOrDefaultAsync(
            user => user.Email == email,
            cancellationToken);

    public async Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        await context.UserProfiles
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToArrayAsync(cancellationToken);
}

public sealed class WorkspaceInvitationRepository(TodoAppDbContext context)
    : IWorkspaceInvitationRepository
{
    public async Task AddAsync(
        WorkspaceInvitation invitation,
        CancellationToken cancellationToken)
    {
        await context.WorkspaceInvitations.AddAsync(
            invitation,
            cancellationToken);
    }

    public Task<WorkspaceInvitation?> GetByTokenAsync(
        string token,
        CancellationToken cancellationToken) =>
        context.WorkspaceInvitations.SingleOrDefaultAsync(
            invitation => invitation.Token == token,
            cancellationToken);

    public Task<WorkspaceInvitation?> GetByIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken) =>
        context.WorkspaceInvitations.SingleOrDefaultAsync(
            invitation => invitation.Id == invitationId,
            cancellationToken);

    public async Task<IReadOnlyList<WorkspaceInvitation>> ListForWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var invitations = await context.WorkspaceInvitations
            .AsNoTracking()
            .Where(invitation => invitation.WorkspaceId == workspaceId)
            .ToArrayAsync(cancellationToken);

        return invitations
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToArray();
    }
}

public sealed class AccountRepository(TodoAppDbContext context)
    : IAccountRepository
{
    public Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken) =>
        context.UserProfiles.AnyAsync(
            user => user.Email == email,
            cancellationToken);

    public async Task<AccountRecord?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await context.UserProfiles.SingleOrDefaultAsync(
            user => user.Email == email,
            cancellationToken);
        if (user is null)
        {
            return null;
        }

        var credential = await context.UserCredentials.FindAsync(
            [user.Id],
            cancellationToken);
        return credential is null
            ? null
            : new AccountRecord(user, credential.PasswordHash);
    }

    public async Task AddAsync(
        UserProfile user,
        Workspace workspace,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await context.UserProfiles.AddAsync(user, cancellationToken);
        await context.Workspaces.AddAsync(workspace, cancellationToken);
        await context.UserCredentials.AddAsync(
            new UserCredential(user.Id, passwordHash),
            cancellationToken);
    }

    public async Task AddUserAsync(
        UserProfile user,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await context.UserProfiles.AddAsync(user, cancellationToken);
        await context.UserCredentials.AddAsync(
            new UserCredential(user.Id, passwordHash),
            cancellationToken);
    }
}
