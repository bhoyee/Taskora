using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for the <see cref="Workspace"/> aggregate: creation, tracked
/// lookup, cascading deletion of the entire workspace (projects, tasks,
/// invitations, memberships), and per-user listings.
/// </summary>
public sealed class WorkspaceRepository(TodoAppDbContext context)
    : IWorkspaceRepository
{
    /// <summary>Stages a new workspace for insertion; persistence happens on unit-of-work save.</summary>
    public async Task AddAsync(
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        await context.Workspaces.AddAsync(workspace, cancellationToken);
    }

    /// <summary>
    /// Loads a tracked workspace by id for mutation. Explicitly includes the
    /// <c>_memberships</c> backing collection by shadow name since it is not
    /// an owned navigation that loads automatically.
    /// </summary>
    public Task<Workspace?> GetByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken) =>
        context.Workspaces
            .Include("_memberships")
            .SingleOrDefaultAsync(
                workspace => workspace.Id == workspaceId,
                cancellationToken);

    /// <summary>
    /// Deletes an entire workspace and everything under it: task
    /// dependencies (removed first via raw SQL, since they aren't reachable
    /// through an EF navigation and would otherwise violate FK constraints),
    /// then tasks, projects, invitations, and memberships. As with the other
    /// repositories, the dependency-cleanup SQL is duplicated for
    /// Postgres/Npgsql (quoted identifiers) and SQLite (unquoted
    /// identifiers), selected at runtime by provider name.
    /// </summary>
    public async Task RemoveAsync(
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        if (context.Database.ProviderName?.Contains(
                "Npgsql",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                DELETE FROM "TaskDependencies"
                WHERE "TaskId" IN (
                    SELECT "Tasks"."Id"
                    FROM "Tasks"
                    INNER JOIN "Projects"
                        ON "Projects"."Id" = "Tasks"."ProjectId"
                    WHERE "Projects"."WorkspaceId" = {workspace.Id}
                )
                OR "DependencyId" IN (
                    SELECT "Tasks"."Id"
                    FROM "Tasks"
                    INNER JOIN "Projects"
                        ON "Projects"."Id" = "Tasks"."ProjectId"
                    WHERE "Projects"."WorkspaceId" = {workspace.Id}
                )
                """,
                cancellationToken);
        }
        else
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                DELETE FROM TaskDependencies
                WHERE TaskId IN (
                    SELECT Tasks.Id
                    FROM Tasks
                    INNER JOIN Projects
                        ON Projects.Id = Tasks.ProjectId
                    WHERE Projects.WorkspaceId = {workspace.Id}
                )
                OR DependencyId IN (
                    SELECT Tasks.Id
                    FROM Tasks
                    INNER JOIN Projects
                        ON Projects.Id = Tasks.ProjectId
                    WHERE Projects.WorkspaceId = {workspace.Id}
                )
                """,
                cancellationToken);
        }

        var tasks = await context.Tasks
            .Where(task => context.Projects.Any(project =>
                project.Id == task.ProjectId &&
                project.WorkspaceId == workspace.Id))
            .ToArrayAsync(cancellationToken);
        var projects = await context.Projects
            .Where(project => project.WorkspaceId == workspace.Id)
            .ToArrayAsync(cancellationToken);
        var invitations = await context.WorkspaceInvitations
            .Where(invitation => invitation.WorkspaceId == workspace.Id)
            .ToArrayAsync(cancellationToken);
        var memberships = await context.WorkspaceMemberships
            .Where(membership => membership.WorkspaceId == workspace.Id)
            .ToArrayAsync(cancellationToken);

        context.Tasks.RemoveRange(tasks);
        context.Projects.RemoveRange(projects);
        context.WorkspaceInvitations.RemoveRange(invitations);
        context.WorkspaceMemberships.RemoveRange(memberships);
        context.Workspaces.Remove(workspace);
    }

    /// <summary>
    /// Lists non-suspended workspaces the given user is a member of. The
    /// membership check is a correlated <c>Any()</c> subquery against
    /// <c>WorkspaceMemberships</c> rather than a navigation predicate, and
    /// <c>_memberships</c> is still explicitly included (by shadow name) so
    /// the returned aggregates carry their membership collections despite
    /// the <c>AsNoTracking()</c> read.
    /// </summary>
    public async Task<IReadOnlyList<Workspace>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await context.Workspaces
            .AsNoTracking()
            .Include("_memberships")
            .Where(workspace =>
                workspace.SuspendedAt == null &&
                context.WorkspaceMemberships.Any(membership =>
                    membership.WorkspaceId == workspace.Id &&
                    membership.UserId == userId))
            .OrderBy(workspace => workspace.Name)
            .ToArrayAsync(cancellationToken);
}

/// <summary>
/// Repository for the <see cref="UserProfile"/> entity, providing lookup by
/// email and bulk lookup by id.
/// </summary>
public sealed class UserProfileRepository(TodoAppDbContext context)
    : IUserProfileRepository
{
    /// <summary>Finds a tracked user profile by exact email match.</summary>
    public Task<UserProfile?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken) =>
        context.UserProfiles.SingleOrDefaultAsync(
            user => user.Email == email,
            cancellationToken);

    /// <summary>Bulk-loads user profiles for a set of ids as a read-only projection.</summary>
    public async Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        await context.UserProfiles
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToArrayAsync(cancellationToken);
}

/// <summary>
/// Repository for the <see cref="WorkspaceInvitation"/> entity: creation,
/// lookup by token or id, and per-workspace listing.
/// </summary>
public sealed class WorkspaceInvitationRepository(TodoAppDbContext context)
    : IWorkspaceInvitationRepository
{
    /// <summary>Stages a new invitation for insertion; persistence happens on unit-of-work save.</summary>
    public async Task AddAsync(
        WorkspaceInvitation invitation,
        CancellationToken cancellationToken)
    {
        await context.WorkspaceInvitations.AddAsync(
            invitation,
            cancellationToken);
    }

    /// <summary>Finds a tracked invitation by its opaque acceptance token.</summary>
    public Task<WorkspaceInvitation?> GetByTokenAsync(
        string token,
        CancellationToken cancellationToken) =>
        context.WorkspaceInvitations.SingleOrDefaultAsync(
            invitation => invitation.Token == token,
            cancellationToken);

    /// <summary>Loads a tracked invitation by id for mutation.</summary>
    public Task<WorkspaceInvitation?> GetByIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken) =>
        context.WorkspaceInvitations.SingleOrDefaultAsync(
            invitation => invitation.Id == invitationId,
            cancellationToken);

    /// <summary>
    /// Lists all invitations for a workspace, newest first. The ordering is
    /// applied client-side after materializing the <c>AsNoTracking()</c>
    /// result rather than in the query itself.
    /// </summary>
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

/// <summary>
/// Repository spanning <see cref="UserProfile"/> and <see cref="UserCredential"/>
/// for authentication concerns: signup, login lookup, password changes, and
/// password-reset tokens.
/// </summary>
public sealed class AccountRepository(TodoAppDbContext context)
    : IAccountRepository
{
    /// <summary>Checks whether a user profile already exists for the given email.</summary>
    public Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken) =>
        context.UserProfiles.AnyAsync(
            user => user.Email == email,
            cancellationToken);

    /// <summary>
    /// Loads the combined profile + credential record for a user by email,
    /// used during login. Returns <c>null</c> if either the profile or its
    /// credential row is missing (a user without a credential row cannot
    /// authenticate).
    /// </summary>
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
            : new AccountRecord(
                user,
                credential.PasswordHash,
                credential.PasswordResetTokenHash,
                credential.PasswordResetTokenExpiresAt);
    }

    /// <summary>
    /// Loads the combined profile + credential record for a user by id, with
    /// the same null-if-either-missing semantics as <see cref="GetByEmailAsync"/>.
    /// </summary>
    public async Task<AccountRecord?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await context.UserProfiles.SingleOrDefaultAsync(
            user => user.Id == userId,
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
            : new AccountRecord(
                user,
                credential.PasswordHash,
                credential.PasswordResetTokenHash,
                credential.PasswordResetTokenExpiresAt);
    }

    /// <summary>
    /// Registers a brand-new account: stages a new user profile, a new
    /// default workspace for that user, and the credential row together so
    /// they're persisted atomically by the same unit-of-work save.
    /// </summary>
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

    /// <summary>
    /// Adds a user profile and credential without creating a workspace, used
    /// e.g. when inviting a user into an existing workspace rather than
    /// signing up fresh.
    /// </summary>
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

    /// <summary>
    /// Updates a user's password hash, creating the credential row if one
    /// doesn't already exist (defensive fallback for legacy/edge-case
    /// accounts missing a credential record).
    /// </summary>
    public async Task ChangePasswordAsync(
        Guid userId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        var credential = await context.UserCredentials.FindAsync(
            [userId],
            cancellationToken);
        if (credential is null)
        {
            await context.UserCredentials.AddAsync(
                new UserCredential(userId, passwordHash),
                cancellationToken);
            return;
        }

        credential.ChangePasswordHash(passwordHash);
    }

    /// <summary>
    /// Sets a password-reset token hash and expiry on the user's credential
    /// row; a no-op if the user has no credential row.
    /// </summary>
    public async Task SetPasswordResetTokenAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var credential = await context.UserCredentials.FindAsync(
            [userId],
            cancellationToken);
        if (credential is null)
        {
            return;
        }

        credential.SetPasswordResetToken(tokenHash, expiresAt);
    }
}
