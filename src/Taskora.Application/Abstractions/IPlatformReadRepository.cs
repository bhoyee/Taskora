namespace TodoApp.Application.Abstractions;

public sealed record PlatformWorkspaceSummary(
    Guid WorkspaceId,
    string WorkspaceName,
    Guid OwnerId,
    string OwnerName,
    string OwnerEmail,
    bool IsSuspended,
    DateTimeOffset? SuspendedAt,
    int ManagerCount,
    int MemberCount,
    int ProjectCount,
    int SprintCount,
    int TaskCount);

public interface IPlatformReadRepository
{
    Task<IReadOnlyList<PlatformWorkspaceSummary>> ListWorkspaceSummariesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> GetTaskCountsByProjectAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken);
}
