using TodoApp.Domain.Collaboration;

namespace TodoApp.Application.Abstractions;

public interface IWorkspaceRepository
{
    Task AddAsync(
        Workspace workspace,
        CancellationToken cancellationToken);

    Task<Workspace?> GetByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Workspace>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}

public sealed record AccountRecord(UserProfile User, string PasswordHash);

public interface IAccountRepository
{
    Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken);

    Task<AccountRecord?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    Task AddAsync(
        UserProfile user,
        Workspace workspace,
        string passwordHash,
        CancellationToken cancellationToken);
}
