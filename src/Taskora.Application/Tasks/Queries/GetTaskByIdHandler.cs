using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;

namespace TodoApp.Application.Tasks.Queries;

/// <summary>
/// Handles fetching the full details of a single task by its identifier.
/// </summary>
public sealed class GetTaskByIdHandler(
    ITaskReadRepository tasks,
    IClock clock)
{
    /// <summary>
    /// Looks up the task by <see cref="GetTaskByIdQuery.TaskId"/> and, if found, maps it
    /// to a <see cref="TaskDetailsDto"/> using the current UTC date (as returned by
    /// <see cref="IClock"/>) to compute deadline health. Returns a not-found failure
    /// when no task with the given identifier exists.
    /// </summary>
    public async Task<Result<TaskDetailsDto>> HandleAsync(
        GetTaskByIdQuery query,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(
            query.TaskId,
            cancellationToken);

        if (task is null)
        {
            return Result<TaskDetailsDto>.Failure(
                new ApplicationError(
                    "task.not_found",
                    "The task was not found.",
                    ErrorType.NotFound));
        }

        return Result<TaskDetailsDto>.Success(
            TaskDtoMapper.ToDetails(
                task,
                DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)));
    }
}
