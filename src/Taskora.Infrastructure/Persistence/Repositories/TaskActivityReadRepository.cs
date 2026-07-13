using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;

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

    public async Task<PagedResult<WorkspaceActivityRecord>> GetForWorkspaceAsync(
        Guid workspaceId,
        string? type,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from activity in context.TaskActivities.AsNoTracking()
            join task in context.Tasks.AsNoTracking()
                on activity.TaskId equals task.Id
            join project in context.Projects.AsNoTracking()
                on task.ProjectId equals project.Id
            where project.WorkspaceId == workspaceId
            select new
            {
                activity.Sequence,
                activity.TaskId,
                TaskTitle = task.Title,
                task.ProjectId,
                ProjectName = project.Name,
                activity.Actor,
                Action = activity.ActivityType,
                activity.PreviousValue,
                activity.CurrentValue,
                activity.OccurredAt
            };

        if (!string.IsNullOrWhiteSpace(type) &&
            !type.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(activity => activity.Action == type);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(activity => activity.Sequence)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(activity => new WorkspaceActivityRecord(
                activity.Sequence,
                activity.TaskId,
                activity.TaskTitle,
                activity.ProjectId,
                activity.ProjectName,
                activity.Actor,
                activity.Action,
                activity.PreviousValue,
                activity.CurrentValue,
                activity.OccurredAt))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<WorkspaceActivityRecord>(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}
