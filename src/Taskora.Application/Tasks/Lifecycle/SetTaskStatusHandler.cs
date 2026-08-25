using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Lifecycle;

/// <summary>
/// Handles <see cref="SetTaskStatusCommand"/> by moving a task directly to an arbitrary status,
/// without the workflow-specific guards applied by the dedicated lifecycle handlers.
/// </summary>
public sealed class SetTaskStatusHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>
    /// Loads the task and delegates to the domain to move it to the requested status, letting the
    /// domain enforce any transition rules. Returns a <see cref="Result{T}"/> with the resulting
    /// status on success, or a failure with a not-found error if the task does not exist.
    /// </summary>
    public async Task<Result<TaskItemStatus>> HandleAsync(
        SetTaskStatusCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(
            command.TaskId,
            cancellationToken);

        if (task is null)
        {
            return Result<TaskItemStatus>.Failure(
                TaskOperationErrors.TaskNotFound());
        }

        task.MoveToStatus(command.Status, clock.UtcNow, command.BlockedReason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<TaskItemStatus>.Success(task.Status);
    }
}
