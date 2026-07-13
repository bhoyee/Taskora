using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class PortfolioDashboardReadRepository(
    TodoAppDbContext context,
    IClock clock,
    ICurrentUser currentUser)
    : IPortfolioDashboardReadRepository
{
    public async Task<PortfolioDashboardSnapshot> GetAsync(
        Guid? workspaceId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var scopedTasks = context.Tasks.AsNoTracking();
        if (workspaceId.HasValue)
        {
            scopedTasks = scopedTasks.Where(task =>
                context.Projects.Any(project =>
                    project.Id == task.ProjectId &&
                    project.WorkspaceId == workspaceId.Value));
        }

        if (projectId.HasValue)
        {
            scopedTasks = scopedTasks.Where(task => task.ProjectId == projectId.Value);
        }

        var active = scopedTasks.Where(task => task.Status != TaskItemStatus.Completed);

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
        var dueTaskValues = await scopedTasks
            .Where(task => task.DueDate != null)
            .Select(task => new
            {
                task.Id,
                task.ProjectId,
                task.Title,
                task.DueDate,
                task.Status,
                task.AssignedUserId
            })
            .ToArrayAsync(cancellationToken);
        var taskAnalytics = await scopedTasks
            .Select(task => new
            {
                task.Status,
                task.DueDate,
                PriorityBand = EF.Property<PriorityScore>(task, "_priority") == null
                    ? (PriorityBand?)null
                    : EF.Property<PriorityScore>(task, "_priority").Band
            })
            .ToArrayAsync(cancellationToken);
        var overdueCount = dueTaskValues.Count(task =>
            task.DueDate!.IsOverdue(today, task.Status));
        var statusBreakdown = Enum.GetValues<TaskItemStatus>()
            .Select(status => new DashboardBreakdownItem(
                status.ToString(),
                taskAnalytics.Count(task => task.Status == status)))
            .ToArray();
        var priorityBreakdown = Enum.GetValues<PriorityBand>()
            .Select(band => new DashboardBreakdownItem(
                band.ToString(),
                taskAnalytics.Count(task => task.PriorityBand == band)))
            .ToArray();
        var deadlineBreakdown = new[]
        {
            new DashboardBreakdownItem(
                "Overdue",
                taskAnalytics.Count(task =>
                    task.DueDate?.IsOverdue(today, task.Status) == true)),
            new DashboardBreakdownItem(
                "Due today",
                taskAnalytics.Count(task =>
                    task.Status != TaskItemStatus.Completed &&
                    task.DueDate?.Value == today)),
            new DashboardBreakdownItem(
                "Due in 7 days",
                taskAnalytics.Count(task =>
                    task.Status != TaskItemStatus.Completed &&
                    task.DueDate is not null &&
                    task.DueDate.Value > today &&
                    task.DueDate.Value <= today.AddDays(7))),
            new DashboardBreakdownItem(
                "Healthy",
                taskAnalytics.Count(task =>
                    task.Status != TaskItemStatus.Completed &&
                    (task.DueDate is null || task.DueDate.Value > today.AddDays(7))))
        };
        var totalTasks = taskAnalytics.Length;
        var completedTasks = taskAnalytics.Count(
            task => task.Status == TaskItemStatus.Completed);
        var completionPercentage = totalTasks == 0
            ? 0
            : (int)Math.Round(completedTasks * 100m / totalTasks);
        var projectProgress = new DashboardProjectProgress(
            completedTasks,
            totalTasks,
            completionPercentage);
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
        var assignmentWarnings = dueTaskValues
            .Where(task =>
                currentUser.IsAuthenticated &&
                task.AssignedUserId == currentUser.UserId &&
                task.Status != TaskItemStatus.Completed)
            .Select(task => new DashboardWarning(
                "TaskAssigned",
                "info",
                "Task assigned to you",
                $"{task.Title} is assigned to you.",
                ProjectId: task.ProjectId,
                TaskId: task.Id,
                DueDate: task.DueDate?.Value));

        return new PortfolioDashboardSnapshot(
            projectCount,
            activeCount,
            blockedCount,
            overdueCount,
            criticalCount,
            statusBreakdown,
            priorityBreakdown,
            deadlineBreakdown,
            projectProgress,
            taskWarnings
                .Concat(projectWarnings)
                .Concat(assignmentWarnings)
                .ToArray());
    }
}
