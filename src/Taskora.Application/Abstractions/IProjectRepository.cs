using TodoApp.Domain.Projects;

namespace TodoApp.Application.Abstractions;

/// <summary>
/// Repository abstraction for persisting and retrieving <see cref="Project"/> aggregates.
/// </summary>
public interface IProjectRepository
{
    /// <summary>
    /// Registers a new project to be inserted when changes are persisted.
    /// </summary>
    /// <param name="project">The project to add.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task AddAsync(Project project, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the project with the given identifier for the purpose of loading and modifying it.
    /// </summary>
    /// <param name="projectId">The identifier of the project to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching project, or null if no project with that identifier exists.</returns>
    Task<Project?> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Registers an existing project to be removed when changes are persisted.
    /// </summary>
    /// <param name="project">The project to remove.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task RemoveAsync(
        Project project,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves all projects belonging to the given workspace.
    /// </summary>
    /// <param name="workspaceId">The identifier of the workspace whose projects are being retrieved.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The projects belonging to the workspace.</returns>
    Task<IReadOnlyList<Project>> ListForWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);
}
