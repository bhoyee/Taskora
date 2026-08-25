using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Lifecycle;

/// <summary>
/// Handles <see cref="StartTaskCommand"/> by moving a task into active work, self-assigning it to
/// the current user first if it has no assignee yet.
/// </summary>
public sealed class StartTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Loads the task, verifies the current user is allowed to start it (unassigned tasks can be
    /// picked up; tasks assigned to someone else cannot), auto-assigns the current user when the
    /// task is unassigned, then applies the domain start rule and persists the change. Returns a
    /// <see cref="Result{T}"/> with the resulting status on success, or a failure carrying a
    /// not-found, authorization, or conflict error (e.g. starting from an invalid status).
    /// </summary>
    public async Task<Result<TaskItemStatus>> HandleAsync(
        StartTaskCommand command,
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

        var authorization = AssignedTaskAuthorization.EnsureCanStart(
            task,
            currentUser);
        if (!authorization.IsSuccess)
        {
            return Result<TaskItemStatus>.Failure(authorization.Error);
        }

        try
        {
            if (task.AssignedUserId is null)
            {
                task.Assign(currentUser.UserId);
            }

            task.Start();
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
