using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Lifecycle;

/// <summary>
/// Handles <see cref="CompleteTaskCommand"/> by transitioning a task to its completed status.
/// </summary>
public sealed class CompleteTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Loads the task, verifies the current user is the assigned worker (only the assignee may
    /// complete the task), then applies the domain completion rule and persists the change.
    /// Returns a <see cref="Result{T}"/> with the resulting status on success, or a failure carrying
    /// a not-found, authorization, or conflict error (e.g. completing from an invalid status).
    /// </summary>
    public async Task<Result<TaskItemStatus>> HandleAsync(
        CompleteTaskCommand command,
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

        var authorization = AssignedTaskAuthorization.EnsureAssignedWorker(
            task,
            currentUser);
        if (!authorization.IsSuccess)
        {
            return Result<TaskItemStatus>.Failure(authorization.Error);
        }

        try
        {
            task.Complete(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<TaskItemStatus>.Success(task.Status);
        }
        catch (DomainRuleException exception)
        {
            return Result<TaskItemStatus>.Failure(
                TaskOperationErrors.From(exception));
        }
    }
}
