using TodoApp.Application.Tasks.Queries;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Abstractions;

/// <summary>
/// Read-only repository for querying <see cref="TaskItem"/> aggregates, used for
/// read/query scenarios that do not require change tracking.
/// </summary>
public interface ITaskReadRepository
{
    /// <summary>
    /// Retrieves the task with the given identifier.
    /// </summary>
    /// <param name="taskId">The identifier of the task to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching task, or null if no task with that identifier exists.</returns>
    Task<TaskItem?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Searches for tasks matching the given filter, sort, and paging criteria.
    /// </summary>
    /// <param name="criteria">The search, filter, and paging criteria to apply.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching page of tasks along with search metadata.</returns>
    Task<TaskSearchResult> SearchAsync(
        TaskSearchCriteria criteria,
        CancellationToken cancellationToken);
}
