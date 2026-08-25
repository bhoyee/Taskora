using TodoApp.Domain.Collaboration;

namespace TodoApp.Application.Abstractions;

/// <summary>
/// Repository abstraction for persisting and retrieving <see cref="Workspace"/> aggregates.
/// </summary>
public interface IWorkspaceRepository
{
    /// <summary>
    /// Registers a new workspace to be inserted when changes are persisted.
    /// </summary>
    /// <param name="workspace">The workspace to add.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task AddAsync(
        Workspace workspace,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the workspace with the given identifier for the purpose of loading and modifying it.
    /// </summary>
    /// <param name="workspaceId">The identifier of the workspace to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching workspace, or null if no workspace with that identifier exists.</returns>
    Task<Workspace?> GetByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Registers an existing workspace to be removed when changes are persisted.
    /// </summary>
    /// <param name="workspace">The workspace to remove.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task RemoveAsync(
        Workspace workspace,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves all workspaces that the given user belongs to.
    /// </summary>
    /// <param name="userId">The identifier of the user whose workspaces are being retrieved.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The workspaces the user belongs to.</returns>
    Task<IReadOnlyList<Workspace>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Repository abstraction for retrieving <see cref="UserProfile"/> records.
/// </summary>
public interface IUserProfileRepository
{
    /// <summary>
    /// Retrieves the user profile with the given email address.
    /// </summary>
    /// <param name="email">The email address to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching user profile, or null if no user with that email exists.</returns>
    Task<UserProfile?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the user profiles matching the given identifiers.
    /// </summary>
    /// <param name="userIds">The identifiers of the users to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The user profiles matching the given identifiers.</returns>
    Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}

/// <summary>
/// Repository abstraction for persisting and retrieving <see cref="WorkspaceInvitation"/> entities.
/// </summary>
public interface IWorkspaceInvitationRepository
{
    /// <summary>
    /// Registers a new workspace invitation to be inserted when changes are persisted.
    /// </summary>
    /// <param name="invitation">The invitation to add.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task AddAsync(
        WorkspaceInvitation invitation,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the workspace invitation associated with the given token.
    /// </summary>
    /// <param name="token">The invitation token to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching invitation, or null if no invitation with that token exists.</returns>
    Task<WorkspaceInvitation?> GetByTokenAsync(
        string token,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the workspace invitation with the given identifier.
    /// </summary>
    /// <param name="invitationId">The identifier of the invitation to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching invitation, or null if no invitation with that identifier exists.</returns>
    Task<WorkspaceInvitation?> GetByIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves all invitations issued for the given workspace.
    /// </summary>
    /// <param name="workspaceId">The identifier of the workspace whose invitations are being retrieved.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The invitations issued for the workspace.</returns>
    Task<IReadOnlyList<WorkspaceInvitation>> ListForWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);
}

public sealed record AccountRecord(
    UserProfile User,
    string PasswordHash,
    string? PasswordResetTokenHash,
    DateTimeOffset? PasswordResetTokenExpiresAt);

/// <summary>
/// Repository abstraction for persisting and retrieving user account data,
/// including credentials and password reset state.
/// </summary>
public interface IAccountRepository
{
    /// <summary>
    /// Determines whether an account already exists for the given email address.
    /// </summary>
    /// <param name="email">The email address to check.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>True if an account with that email exists; otherwise, false.</returns>
    Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the account associated with the given email address.
    /// </summary>
    /// <param name="email">The email address to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching account record, or null if no account with that email exists.</returns>
    Task<AccountRecord?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the account with the given user identifier.
    /// </summary>
    /// <param name="userId">The identifier of the user account to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching account record, or null if no account with that identifier exists.</returns>
    Task<AccountRecord?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new user together with their initial workspace and credentials.
    /// </summary>
    /// <param name="user">The user profile to add.</param>
    /// <param name="workspace">The initial workspace to create for the user.</param>
    /// <param name="passwordHash">The hashed password to store for the user.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task AddAsync(
        UserProfile user,
        Workspace workspace,
        string passwordHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new user account without an accompanying workspace.
    /// </summary>
    /// <param name="user">The user profile to add.</param>
    /// <param name="passwordHash">The hashed password to store for the user.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task AddUserAsync(
        UserProfile user,
        string passwordHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the stored password hash for the given user.
    /// </summary>
    /// <param name="userId">The identifier of the user whose password is being changed.</param>
    /// <param name="passwordHash">The new hashed password to store.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task ChangePasswordAsync(
        Guid userId,
        string passwordHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores a password reset token for the given user, valid until the given expiry time.
    /// </summary>
    /// <param name="userId">The identifier of the user requesting a password reset.</param>
    /// <param name="tokenHash">The hashed password reset token to store.</param>
    /// <param name="expiresAt">The time at which the reset token expires.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task SetPasswordResetTokenAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}
