using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Projects.Board;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only repository that builds the Kanban board snapshot (status
/// counts, overdue/at-risk/critical counts, and high-priority blocked
/// tasks) for a single project.
/// </summary>
public sealed class ProjectBoardReadRepository(
    TodoAppDbContext context,
    IClock clock)
    : IProjectBoardReadRepository
{
    /// <summary>
    /// Assembles a <see cref="ProjectBoardSnapshot"/> for the given project by
    /// combining a grouped status-count query, two raw SQL queries for
    /// overdue/at-risk task counts, and an EF LINQ query for critical and
    /// high-priority-blocked tasks.
    /// The overdue and at-risk counts use <c>SqlQueryRaw</c> against the
    /// <c>Tasks</c> table directly (parameterized, so it is safe from
    /// injection) rather than LINQ, since the same date-comparison logic is
    /// simplest expressed as raw SQL that works identically against both the
    /// Postgres and SQLite providers.
    /// The priority and dependency lookups use <c>EF.Property</c> to reach
    /// the shadow/backing fields <c>_priority</c> and <c>_dependencies</c>,
    /// which are not exposed as public navigation/properties on
    /// <see cref="TaskItem"/>; <c>_dependencies</c> additionally requires an
    /// explicit <c>Include("_dependencies")</c> because it is not an owned
    /// navigation and would not otherwise be loaded.
    /// </summary>
    public async Task<ProjectBoardSnapshot> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var projectTasks = context.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId);

        var counts = await projectTasks
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Backlog = group.Count(
                    task => task.Status == TaskItemStatus.Backlog),
                Ready = group.Count(
                    task => task.Status == TaskItemStatus.Ready),
                InProgress = group.Count(
                    task => task.Status == TaskItemStatus.InProgress),
                Blocked = group.Count(
                    task => task.Status == TaskItemStatus.Blocked),
                Completed = group.Count(
                    task => task.Status == TaskItemStatus.Completed)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var overdueCount = await context.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS Value
                FROM Tasks
                WHERE ProjectId = {0}
                  AND DueDate IS NOT NULL
                  AND DueDate < {1}
                  AND Status <> {2}
                """,
                projectId,
                today,
                (int)TaskItemStatus.Completed)
            .SingleAsync(cancellationToken);
        var atRiskUntil = today.AddDays(3);
        var atRiskCount = await context.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS Value
                FROM Tasks
                WHERE ProjectId = {0}
                  AND DueDate >= {1}
                  AND DueDate <= {2}
                  AND Status <> {3}
                """,
                projectId,
                today,
                atRiskUntil,
                (int)TaskItemStatus.Completed)
            .SingleAsync(cancellationToken);
        var criticalCount = await projectTasks.CountAsync(
            task =>
                EF.Property<PriorityScore>(task, "_priority").Band ==
                    PriorityBand.Critical &&
                task.Status != TaskItemStatus.Completed,
            cancellationToken);

        var highPriorityBlockedTasks = await projectTasks
            .Where(task =>
                (task.Status == TaskItemStatus.Blocked ||
                 EF.Property<ICollection<TaskItem>>(
                         task,
                         "_dependencies")
                     .Any(dependency =>
                         dependency.Status != TaskItemStatus.Completed)) &&
                EF.Property<PriorityScore>(task, "_priority").Band >=
                    PriorityBand.High)
            .Include("_dependencies")
            .OrderByDescending(task =>
                EF.Property<PriorityScore>(task, "_priority").Value)
            .ToArrayAsync(cancellationToken);

        return new ProjectBoardSnapshot(
            counts?.Backlog ?? 0,
            counts?.Ready ?? 0,
            counts?.InProgress ?? 0,
            counts?.Blocked ?? 0,
            counts?.Completed ?? 0,
            overdueCount,
            atRiskCount,
            criticalCount,
            highPriorityBlockedTasks);
    }
}
