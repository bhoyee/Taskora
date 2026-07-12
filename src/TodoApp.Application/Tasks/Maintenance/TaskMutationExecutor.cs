using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Maintenance;

internal static class TaskMutationExecutor
{
    public static async Task<Result<TaskItemStatus>> ExecuteAsync(
        Guid taskId,
        ITaskRepository tasks,
        IUnitOfWork unitOfWork,
        Action<TaskItem> mutation,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(taskId, cancellationToken);

        if (task is null)
        {
            return Result<TaskItemStatus>.Failure(
                new ApplicationError(
                    "task.not_found",
                    "The task was not found.",
                    ErrorType.NotFound));
        }

        try
        {
            return await ExecuteLoadedAsync(
                task,
                taskId,
                unitOfWork,
                mutation,
                cancellationToken);
        }
        catch (DomainValidationException exception)
        {
            return Result<TaskItemStatus>.Failure(
                new ApplicationError(
                    "task.validation",
                    exception.Message,
                    ErrorType.Validation));
        }
        catch (DomainRuleException exception)
        {
            return Result<TaskItemStatus>.Failure(
                new ApplicationError(
                    "task.conflict",
                    exception.Message,
                    ErrorType.Conflict));
        }
    }

    public static async Task<Result<TaskItemStatus>> ExecuteLoadedAsync(
        TaskItem task,
        Guid taskId,
        IUnitOfWork unitOfWork,
        Action<TaskItem> mutation,
        CancellationToken cancellationToken)
    {
        try
        {
            mutation(task);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<TaskItemStatus>.Success(task.Status);
        }
        catch (DomainValidationException exception)
        {
            return Result<TaskItemStatus>.Failure(
                new ApplicationError(
                    "task.validation",
                    exception.Message,
                    ErrorType.Validation));
        }
        catch (DomainRuleException exception)
        {
            return Result<TaskItemStatus>.Failure(
                new ApplicationError(
                    "task.conflict",
                    exception.Message,
                    ErrorType.Conflict));
        }
    }
}
