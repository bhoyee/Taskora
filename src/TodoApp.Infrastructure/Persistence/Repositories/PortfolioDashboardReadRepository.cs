using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class PortfolioDashboardReadRepository(
    TodoAppDbContext context,
    IClock clock)
    : IPortfolioDashboardReadRepository
{
    public async Task<PortfolioDashboardSnapshot> GetAsync(
        CancellationToken cancellationToken)
    {
        var active = context.Tasks
            .AsNoTracking()
            .Where(task => task.Status != TaskItemStatus.Completed);
        var projectCount = await context.Projects
            .AsNoTracking()
            .CountAsync(
                project => project.ArchivedAt == null,
                cancellationToken);
        var activeCount = await active.CountAsync(cancellationToken);
        var blockedCount = await active.CountAsync(
            task =>
                task.Status == TaskItemStatus.Blocked ||
                EF.Property<ICollection<TaskItem>>(task, "_dependencies")
                    .Any(dependency =>
                        dependency.Status != TaskItemStatus.Completed),
            cancellationToken);
        var criticalCount = await active.CountAsync(
            task => EF.Property<PriorityScore>(task, "_priority").Band ==
                    PriorityBand.Critical,
            cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var overdueCount = await context.Database.SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS Value
                FROM Tasks
                WHERE DueDate IS NOT NULL
                  AND DueDate < {0}
                  AND Status <> {1}
                """,
                today,
                (int)TaskItemStatus.Completed)
            .SingleAsync(cancellationToken);

        return new PortfolioDashboardSnapshot(
            projectCount,
            activeCount,
            blockedCount,
            overdueCount,
            criticalCount);
    }
}
