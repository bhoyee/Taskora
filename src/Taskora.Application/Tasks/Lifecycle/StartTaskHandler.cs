using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Lifecycle;

public sealed class StartTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
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
