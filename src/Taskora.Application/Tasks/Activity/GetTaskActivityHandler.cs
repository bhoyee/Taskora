using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;

namespace TodoApp.Application.Tasks.Activity;

/// <summary>
/// Query requesting the activity history for the task identified by <paramref name="TaskId"/>.
/// </summary>
public sealed record GetTaskActivityQuery(Guid TaskId);

/// <summary>
/// Handles fetching the activity log for a single task.
/// </summary>
public sealed class GetTaskActivityHandler(
    ITaskReadRepository tasks,
    ITaskActivityReadRepository activity)
{
    /// <summary>
    /// Confirms the task exists, then returns its full activity history as recorded
    /// by <see cref="ITaskActivityReadRepository"/>. Fails with not-found if the task
    /// does not exist.
    /// </summary>
    public async Task<Result<IReadOnlyList<TaskActivityRecord>>> HandleAsync(
        GetTaskActivityQuery query,
        CancellationToken cancellationToken)
    {
        if (await tasks.GetByIdAsync(query.TaskId, cancellationToken) is null)
        {
            return Result<IReadOnlyList<TaskActivityRecord>>.Failure(
                new ApplicationError(
                    "task.not_found",
                    "The task was not found.",
                    ErrorType.NotFound));
        }

        return Result<IReadOnlyList<TaskActivityRecord>>.Success(
            await activity.GetForTaskAsync(
                query.TaskId,
                cancellationToken));
    }
}
