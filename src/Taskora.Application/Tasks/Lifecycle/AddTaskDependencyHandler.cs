using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;

namespace TodoApp.Application.Tasks.Lifecycle;

public sealed class AddTaskDependencyHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<bool>> HandleAsync(
        AddTaskDependencyCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(
            command.TaskId,
            cancellationToken);

        if (task is null)
        {
            return Result<bool>.Failure(
                TaskOperationErrors.TaskNotFound());
        }

        var dependency = await tasks.GetByIdAsync(
            command.DependencyId,
            cancellationToken);

        if (dependency is null)
        {
            return Result<bool>.Failure(
                TaskOperationErrors.DependencyNotFound());
        }

        try
        {
            task.AddDependency(dependency);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (DomainRuleException exception)
        {
            return Result<bool>.Failure(
                TaskOperationErrors.From(exception));
        }
        catch (DomainValidationException exception)
        {
            return Result<bool>.Failure(
                TaskOperationErrors.From(exception));
        }
    }
}
