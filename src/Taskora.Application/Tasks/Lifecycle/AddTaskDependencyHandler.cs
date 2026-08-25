using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;

namespace TodoApp.Application.Tasks.Lifecycle;

/// <summary>
/// Handles <see cref="AddTaskDependencyCommand"/> by linking one task as a prerequisite of another.
/// </summary>
public sealed class AddTaskDependencyHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Loads the target task and the proposed dependency, then delegates to the domain to enforce
    /// dependency rules (e.g. no self- or circular dependencies) before persisting the change.
    /// Returns a <see cref="Result{T}"/> that fails with a not-found error if either task does not
    /// exist, or with a conflict/validation error if the domain rejects the dependency; otherwise
    /// succeeds with <see langword="true"/>.
    /// </summary>
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
