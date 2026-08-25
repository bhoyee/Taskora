using TodoApp.Application.Projects.Board;

namespace TodoApp.Application.Abstractions;

/// <summary>
/// Read-only repository for building the board (Kanban-style) view of a project's tasks.
/// </summary>
public interface IProjectBoardReadRepository
{
    /// <summary>
    /// Builds a snapshot of a project's board, including its tasks grouped by status/column.
    /// </summary>
    /// <param name="projectId">The identifier of the project whose board is being retrieved.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The project's board snapshot.</returns>
    Task<ProjectBoardSnapshot> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken);
}
