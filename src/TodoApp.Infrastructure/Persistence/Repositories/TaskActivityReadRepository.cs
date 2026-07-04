using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class TaskActivityReadRepository(TodoAppDbContext context)
    : ITaskActivityReadRepository
{
    public async Task<IReadOnlyList<TaskActivityRecord>> GetForTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken) =>
        await context.TaskActivities
            .AsNoTracking()
            .Where(activity => activity.TaskId == taskId)
            .OrderByDescending(activity => activity.Sequence)
            .Select(activity => new TaskActivityRecord(
                activity.Sequence,
                activity.TaskId,
                activity.Actor,
                activity.ActivityType,
                activity.PreviousValue,
                activity.CurrentValue,
                activity.OccurredAt))
            .ToArrayAsync(cancellationToken);
}
