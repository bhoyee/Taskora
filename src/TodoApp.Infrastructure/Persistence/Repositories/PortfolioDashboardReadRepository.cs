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
        Guid? workspaceId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var active = context.Tasks
            .AsNoTracking()
            .Where(task => task.Status != TaskItemStatus.Completed);
        if (workspaceId.HasValue)
        {
            active = active.Where(task =>
                context.Projects.Any(project =>
                    project.Id == task.ProjectId &&
                    project.WorkspaceId == workspaceId.Value));
        }

        if (projectId.HasValue)
        {
            active = active.Where(task => task.ProjectId == projectId.Value);
        }

        var projects = context.Projects.AsNoTracking();
        if (workspaceId.HasValue)
        {
            projects = projects.Where(
                project => project.WorkspaceId == workspaceId.Value);
        }

        if (projectId.HasValue)
        {
            projects = projects.Where(project => project.Id == projectId.Value);
        }

        var projectCount = await projects.CountAsync(
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
        var dueTasks = context.Tasks.AsNoTracking()
            .Where(task => task.DueDate != null);
        if (workspaceId.HasValue)
        {
            dueTasks = dueTasks.Where(task =>
                context.Projects.Any(project =>
                    project.Id == task.ProjectId &&
                    project.WorkspaceId == workspaceId.Value));
        }

        if (projectId.HasValue)
        {
            dueTasks = dueTasks.Where(task => task.ProjectId == projectId.Value);
        }

        var dueTaskValues = await dueTasks
            .Select(task => new
            {
                task.Id,
                task.ProjectId,
                task.Title,
                task.DueDate,
                task.Status
            })
            .ToArrayAsync(cancellationToken);
        var overdueCount = dueTaskValues.Count(task =>
            task.DueDate!.IsOverdue(today, task.Status));
        var tomorrow = today.AddDays(1);
        var twoDaysFromNow = today.AddDays(2);
        var taskWarnings = dueTaskValues
            .Where(task => task.Status != TaskItemStatus.Completed)
            .Where(task =>
                task.DueDate!.Value == today ||
                task.DueDate.Value == tomorrow ||
                task.DueDate.Value == twoDaysFromNow)
            .Select(task =>
            {
                var dueDate = task.DueDate!.Value;
                var (severity, message) = dueDate == today
                    ? ("critical", $"{task.Title} is due today.")
                    : dueDate == tomorrow
                        ? ("warning", $"{task.Title} is due in 24 hours.")
                        : ("info", $"{task.Title} is due in 2 days.");
                return new DashboardWarning(
                    "TaskDue",
                    severity,
                    "Task deadline reminder",
                    message,
                    ProjectId: task.ProjectId,
                    TaskId: task.Id,
                    DueDate: dueDate);
            });
        var projectValues = await projects
            .Where(project => project.TargetDate != null)
            .Select(project => new
            {
                project.Id,
                project.Name,
                project.TargetDate,
                project.ArchivedAt
            })
            .ToArrayAsync(cancellationToken);
        var projectWarnings = projectValues
            .Where(project => project.ArchivedAt is null)
            .Where(project => project.TargetDate!.Value == tomorrow)
            .Select(project => new DashboardWarning(
                "ProjectTarget",
                "warning",
                "Project delivery date reminder",
                $"{project.Name} reaches its delivery date in 24 hours.",
                ProjectId: project.Id,
                DueDate: project.TargetDate!.Value));

        return new PortfolioDashboardSnapshot(
            projectCount,
            activeCount,
            blockedCount,
            overdueCount,
            criticalCount,
            taskWarnings.Concat(projectWarnings).ToArray());
    }
}
