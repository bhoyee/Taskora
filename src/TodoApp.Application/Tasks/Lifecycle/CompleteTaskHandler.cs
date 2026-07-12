using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Lifecycle;

public sealed class CompleteTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
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
