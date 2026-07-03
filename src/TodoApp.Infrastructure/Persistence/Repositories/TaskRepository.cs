using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Tasks.Queries;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class TaskRepository(TodoAppDbContext context)
    : ITaskRepository, ITaskReadRepository
{
    public async Task AddAsync(
        TaskItem task,
        CancellationToken cancellationToken)
    {
        await context.Tasks.AddAsync(task, cancellationToken);
    }

    public Task<TaskItem?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken) =>
        context.Tasks
            .Include("_dependencies")
            .SingleOrDefaultAsync(
                task => task.Id == taskId,
                cancellationToken);

    public async Task<TaskSearchResult> SearchAsync(
        TaskSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        IQueryable<TaskItem> query = context.Tasks
            .AsNoTracking();

        if (criteria.ProjectId.HasValue)
        {
            query = query.Where(
                task => task.ProjectId == criteria.ProjectId.Value);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(
                task => task.Status == criteria.Status.Value);
        }

        if (criteria.IsBlocked.HasValue)
        {
            query = criteria.IsBlocked.Value
                ? query.Where(task =>
                    task.Status == TaskItemStatus.Blocked ||
                    EF.Property<ICollection<TaskItem>>(
                            task,
                            "_dependencies")
                        .Any(dependency =>
                            dependency.Status != TaskItemStatus.Completed))
                : query.Where(task =>
                    task.Status != TaskItemStatus.Blocked &&
                    !EF.Property<ICollection<TaskItem>>(
                            task,
                            "_dependencies")
                        .Any(dependency =>
                            dependency.Status != TaskItemStatus.Completed));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var search = criteria.Search.Trim();
            query = query.Where(task =>
                EF.Functions.Like(task.Title, $"%{search}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        query = criteria.SortBy switch
        {
            TaskSortBy.DueDateAscending => query
                .OrderBy(task => task.DueDate),
            TaskSortBy.TitleAscending => query
                .OrderBy(task => task.Title),
            _ => query.OrderByDescending(task =>
                EF.Property<PriorityScore>(task, "_priority").Value)
        };

        var items = await query
            .Include("_dependencies")
            .Skip((criteria.PageNumber - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToArrayAsync(cancellationToken);

        return new TaskSearchResult(items, totalCount);
    }
}
