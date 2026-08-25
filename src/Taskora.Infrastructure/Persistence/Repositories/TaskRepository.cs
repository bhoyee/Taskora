using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Tasks.Queries;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for the <see cref="TaskItem"/> aggregate, implementing both the
/// write-side <c>ITaskRepository</c> and the read-side <c>ITaskReadRepository</c>
/// (paged search) contracts.
/// </summary>
public sealed class TaskRepository(TodoAppDbContext context)
    : ITaskRepository, ITaskReadRepository
{
    /// <summary>Stages a new task for insertion; persistence happens on unit-of-work save.</summary>
    public async Task AddAsync(
        TaskItem task,
        CancellationToken cancellationToken)
    {
        await context.Tasks.AddAsync(task, cancellationToken);
    }

    /// <summary>
    /// Deletes a task, first removing any <c>TaskDependencies</c> rows that
    /// reference it (as either the dependent task or the dependency) via raw
    /// SQL, since dependencies are not an EF-owned relationship the delete
    /// can cascade through automatically. The SQL is duplicated with quoted
    /// identifiers for Postgres/Npgsql and unquoted identifiers for SQLite,
    /// chosen at runtime from the active provider name.
    /// </summary>
    public async Task RemoveAsync(
        TaskItem task,
        CancellationToken cancellationToken)
    {
        if (context.Database.ProviderName?.Contains(
                "Npgsql",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                DELETE FROM "TaskDependencies"
                WHERE "TaskId" = {task.Id} OR "DependencyId" = {task.Id}
                """,
                cancellationToken);
        }
        else
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                DELETE FROM TaskDependencies
                WHERE TaskId = {task.Id} OR DependencyId = {task.Id}
                """,
                cancellationToken);
        }

        context.Tasks.Remove(task);
    }

    /// <summary>
    /// Loads a tracked task by id for mutation. Explicitly includes the
    /// <c>_dependencies</c>, <c>_tags</c>, and <c>_notes</c> backing
    /// collections by shadow name, since none of them are owned navigations
    /// that EF would load automatically.
    /// </summary>
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

    /// <summary>
    /// Runs a filtered, sorted, paged search over tasks. Filters
    /// (project/workspace/status/blocked/category/sprint/tag/title search)
    /// are applied incrementally as optional <c>Where</c> clauses; the
    /// workspace filter uses a correlated <c>Any()</c> subquery against
    /// <c>Projects</c> since <see cref="TaskItem"/> has no direct workspace
    /// navigation. The "blocked" and default priority sort both reach the
    /// shadow <c>_dependencies</c>/<c>_priority</c> fields via
    /// <c>EF.Property</c> because they aren't public navigations/properties.
    /// Title search uses <c>EF.Functions.Like</c> so the pattern translates
    /// to a provider-native <c>LIKE</c> on both Postgres and SQLite. The
    /// total count is taken before paging, and the final materialization
    /// re-applies <c>Include("_dependencies")</c>, <c>Include("_tags")</c>,
    /// and <c>Include("_notes")</c> (shadow navigations, not owned) so the
    /// returned page has its collections populated despite the earlier
    /// <c>AsNoTracking()</c> projection-free filtering.
    /// </summary>
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
