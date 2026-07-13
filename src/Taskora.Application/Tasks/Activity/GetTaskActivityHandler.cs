using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;

namespace TodoApp.Application.Tasks.Activity;

public sealed record GetTaskActivityQuery(Guid TaskId);

public sealed class GetTaskActivityHandler(
    ITaskReadRepository tasks,
    ITaskActivityReadRepository activity)
{
    public async Task<Result<IReadOnlyList<TaskActivityRecord>>> HandleAsync(
        GetTaskActivityQuery query,
        CancellationToken cancellationToken)
    {
        if (await tasks.GetByIdAsync(query.TaskId, cancellationToken) is null)
        {
            return Result<IReadOnlyList<TaskActivityRecord>>.Failure(
                new ApplicationError(
                    "task.not_found",
                    "The task was not found.",
                    ErrorType.NotFound));
        }

        return Result<IReadOnlyList<TaskActivityRecord>>.Success(
            await activity.GetForTaskAsync(
                query.TaskId,
                cancellationToken));
    }
}
