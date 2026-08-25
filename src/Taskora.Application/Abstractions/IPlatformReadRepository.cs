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

/// <summary>
/// Read-only repository for platform-wide administrative queries spanning all workspaces,
/// used by platform/admin-level reporting rather than tenant-scoped features.
/// </summary>
public interface IPlatformReadRepository
{
    /// <summary>
    /// Retrieves summary statistics for every workspace on the platform.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The summary of each workspace on the platform.</returns>
    Task<IReadOnlyList<PlatformWorkspaceSummary>> ListWorkspaceSummariesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the task count for each of the given projects.
    /// </summary>
    /// <param name="projectIds">The identifiers of the projects to count tasks for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A dictionary mapping each project identifier to its task count.</returns>
    Task<IReadOnlyDictionary<Guid, int>> GetTaskCountsByProjectAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken);
}
