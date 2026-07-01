using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Lifecycle;

public sealed class StartTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
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

        try
        {
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
