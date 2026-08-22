using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Lifecycle;

public sealed class SetTaskStatusHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    IClock clock)
{
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
