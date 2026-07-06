using TodoApp.Domain.Collaboration;

namespace TodoApp.Application.Abstractions;

public interface IWorkspaceRepository
{
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
