using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;

namespace TodoApp.Application.Tasks.Queries;

public sealed class GetTaskByIdHandler(
    ITaskReadRepository tasks,
    IClock clock)
{
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
