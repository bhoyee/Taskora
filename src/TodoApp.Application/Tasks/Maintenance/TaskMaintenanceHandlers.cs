using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Maintenance;

public sealed class MoveTaskToReadyHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    public Task<Result<TaskItemStatus>> HandleAsync(
        MoveTaskToReadyCommand command,
        CancellationToken cancellationToken) =>
        TaskMutationExecutor.ExecuteAsync(
            command.TaskId,
            tasks,
            unitOfWork,
            task => task.MoveToReady(),
            cancellationToken);
}

public sealed class UpdateTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    public Task<Result<TaskItemStatus>> HandleAsync(
        UpdateTaskCommand command,
        CancellationToken cancellationToken) =>
        TaskMutationExecutor.ExecuteAsync(
            command.TaskId,
            tasks,
            unitOfWork,
            task =>
            {
                task.Rename(command.Title);

                if (command.DueDate.HasValue)
                {
                    task.Schedule(DueDate.Create(command.DueDate.Value));
                }

                if (command.Effort.HasValue)
                {
                    task.Estimate(EffortEstimate.Create(command.Effort.Value));
                }
            },
            cancellationToken);
}

public sealed class BlockTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    public Task<Result<TaskItemStatus>> HandleAsync(
        BlockTaskCommand command,
        CancellationToken cancellationToken) =>
        TaskMutationExecutor.ExecuteAsync(
            command.TaskId,
            tasks,
            unitOfWork,
            task => task.Block(command.Reason),
            cancellationToken);
}

public sealed class UnblockTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    public Task<Result<TaskItemStatus>> HandleAsync(
        UnblockTaskCommand command,
        CancellationToken cancellationToken) =>
        TaskMutationExecutor.ExecuteAsync(
            command.TaskId,
            tasks,
            unitOfWork,
            task => task.Unblock(),
            cancellationToken);
}

public sealed class ReopenTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    public Task<Result<TaskItemStatus>> HandleAsync(
        ReopenTaskCommand command,
        CancellationToken cancellationToken) =>
        TaskMutationExecutor.ExecuteAsync(
            command.TaskId,
            tasks,
            unitOfWork,
            task => task.Reopen(),
            cancellationToken);
}

public sealed class DeleteTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(
        DeleteTaskCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);

        if (task is null)
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "task.not_found",
                    "The task was not found.",
                    ErrorType.NotFound));
        }

        if (!currentUser.IsAuthenticated ||
            task.CreatedByUserId is null ||
            task.CreatedByUserId != currentUser.UserId)
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "task.delete_forbidden",
                    "Only the task creator can delete this task.",
                    ErrorType.Forbidden));
        }

        await tasks.RemoveAsync(task, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class UpdatePlanningFactorsHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task<Result<TaskItemStatus>> HandleAsync(
        UpdatePlanningFactorsCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);

        if (task is null)
        {
            return Result<TaskItemStatus>.Failure(
                new ApplicationError(
                    "task.not_found",
                    "The task was not found.",
                    ErrorType.NotFound));
        }

        if (!currentUser.IsAuthenticated ||
            (task.CreatedByUserId is not null &&
             task.CreatedByUserId != currentUser.UserId))
        {
            return Result<TaskItemStatus>.Failure(
                new ApplicationError(
                    "task.planning_forbidden",
                    "Only the task creator can edit priority inputs.",
                    ErrorType.Forbidden));
        }

        return await TaskMutationExecutor.ExecuteLoadedAsync(
            task,
            command.TaskId,
            unitOfWork,
            item => item.SetPlanningFactors(
                PlanningFactors.Create(
                    command.BusinessValue,
                    command.Urgency,
                    command.RiskReduction,
                    command.Effort)),
            cancellationToken);
    }
}

public sealed class RemoveTaskDependencyHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    public Task<Result<TaskItemStatus>> HandleAsync(
        RemoveTaskDependencyCommand command,
        CancellationToken cancellationToken) =>
        TaskMutationExecutor.ExecuteAsync(
            command.TaskId,
            tasks,
            unitOfWork,
            task => task.RemoveDependency(command.DependencyId),
            cancellationToken);
}
