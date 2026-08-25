using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Maintenance;

/// <summary>
/// Shared execution pipeline for simple task maintenance mutations: loads the task (when needed),
/// applies a domain mutation, persists the change, and translates domain exceptions into
/// <see cref="Result{T}"/> failures with consistent error codes.
/// </summary>
internal static class TaskMutationExecutor
{
    // Standard "task not found" failure result shared by maintenance handlers.
    public static Result<TaskItemStatus> NotFound() =>
        Result<TaskItemStatus>.Failure(
            new ApplicationError(
                "task.not_found",
                "The task was not found.",
                ErrorType.NotFound));

    /// <summary>
    /// Loads the task by id, then applies <paramref name="mutation"/> and saves via
    /// <see cref="ExecuteLoadedAsync"/>. Returns a <see cref="Result{T}"/> with the resulting status
    /// on success, or a failure with a not-found, validation, or conflict error.
    /// </summary>
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
            return NotFound();
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

    /// <summary>
    /// Applies <paramref name="mutation"/> to an already-loaded task and saves the change. Domain
    /// validation and rule violations are caught and mapped to failed <see cref="Result{T}"/>s
    /// (validation or conflict errors respectively); otherwise returns success with the task's
    /// resulting status.
    /// </summary>
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
