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

    public async Task RemoveAsync(
        TaskItem task,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            DELETE FROM TaskDependencies
            WHERE TaskId = {task.Id} OR DependencyId = {task.Id}
            """,
            cancellationToken);
        context.Tasks.Remove(task);
    }

    public Task<TaskItem?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken) =>
        context.Tasks
            .Include("_dependencies")
            .Include("_tags")
            .Include("_notes")
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

        if (criteria.WorkspaceId.HasValue)
        {
            query = query.Where(task =>
                context.Projects.Any(project =>
                    project.Id == task.ProjectId &&
                    project.WorkspaceId == criteria.WorkspaceId.Value));
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

        if (criteria.CategoryId.HasValue)
        {
            query = query.Where(
                task => task.CategoryId == criteria.CategoryId.Value);
        }

        if (criteria.SprintId.HasValue)
        {
            query = query.Where(
                task => task.SprintId == criteria.SprintId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Tag))
        {
            query = query.Where(task =>
                EF.Property<ICollection<TaskTag>>(task, "_tags")
                    .Any(tag => tag.Name == criteria.Tag));
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
            TaskSortBy.CreatedDescending => query
                .OrderByDescending(task => task.CreatedAt)
                .ThenBy(task => task.Id),
            TaskSortBy.DueDateAscending => query
                .OrderBy(task => task.DueDate == null)
                .ThenBy(task => task.DueDate)
                .ThenBy(task => task.CreatedAt)
                .ThenBy(task => task.Id),
            TaskSortBy.TitleAscending => query
                .OrderBy(task => task.Title)
                .ThenBy(task => task.CreatedAt)
                .ThenBy(task => task.Id),
            _ => query.OrderByDescending(task =>
                    EF.Property<PriorityScore>(task, "_priority").Value)
                .ThenBy(task => task.DueDate == null)
                .ThenBy(task => task.DueDate)
                .ThenBy(task => task.CreatedAt)
                .ThenBy(task => task.Id)
        };

        var items = await query
            .Include("_dependencies")
            .Include("_tags")
            .Include("_notes")
            .Skip((criteria.PageNumber - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToArrayAsync(cancellationToken);

        return new TaskSearchResult(items, totalCount);
    }
}
