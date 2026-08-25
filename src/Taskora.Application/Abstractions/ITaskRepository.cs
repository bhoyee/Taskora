using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Abstractions;

/// <summary>
/// Repository abstraction for persisting and retrieving <see cref="TaskItem"/> aggregates.
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// Retrieves the task with the given identifier for the purpose of loading and modifying it.
    /// </summary>
    /// <param name="taskId">The identifier of the task to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching task, or null if no task with that identifier exists.</returns>
    Task<TaskItem?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Registers a new task to be inserted when changes are persisted.
    /// </summary>
    /// <param name="task">The task to add.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task AddAsync(TaskItem task, CancellationToken cancellationToken);

    /// <summary>
    /// Registers an existing task to be removed when changes are persisted.
    /// </summary>
    /// <param name="task">The task to remove.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task RemoveAsync(TaskItem task, CancellationToken cancellationToken);
}
