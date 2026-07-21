using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class WorkspaceReportReadRepository(
    TodoAppDbContext context,
    ICurrentUser currentUser,
    IBusinessDateProvider dates)
    : IWorkspaceReportReadRepository
{
    public async Task<WorkspaceReportSnapshot> GetAsync(
        Guid workspaceId,
        DateOnly? from,
        DateOnly? to,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var today = dates.Today;
        var projectsQuery = context.Projects
            .AsNoTracking()
            .Where(project => project.WorkspaceId == workspaceId);

        if (projectId.HasValue)
        {
            projectsQuery = projectsQuery.Where(project => project.Id == projectId.Value);
        }

        var projectValues = await projectsQuery
            .Select(project => new
            {
                project.Id,
                project.Name,
                project.Description,
                DeliveryDate = project.TargetDate == null
                    ? (DateOnly?)null
                    : project.TargetDate.Value,
                project.ArchivedAt
            })
            .ToArrayAsync(cancellationToken);
        var projects = projectValues
            .Select(project => new ProjectReportValue(
                project.Id,
                project.Name,
                project.Description,
                project.DeliveryDate,
                project.ArchivedAt))
            .ToArray();
        var projectIds = projects.Select(project => project.Id).ToArray();

        var taskEntities = await context.Tasks
            .AsNoTracking()
            .Include("_tags")
            .Include("_dependencies")
            .Where(task => projectIds.Contains(task.ProjectId))
            .ToArrayAsync(cancellationToken);
        var allTaskValues = taskEntities
            .Select(task => new TaskReportValue(
                task.Id,
                task.ProjectId,
                task.AssignedUserId,
                task.Title,
                task.Status,
                task.DueDate?.Value,
                task.CreatedAt,
                task.CompletedAt,
                task.IsBlocked,
                task.HasPlanningFactors ? task.Priority.Value : null,
                task.HasPlanningFactors ? task.Priority.Band : null,
                task.Tags.Select(tag => tag.Name).ToArray()))
            .ToArray();

        var tasksInRange = allTaskValues
            .Where(task => IsTaskInRange(
                task.CreatedAt,
                task.DueDate,
                task.CompletedAt,
                from,
                to))
            .ToArray();
        var projectLookup = projects.ToDictionary(project => project.Id);
        var reportTasks = tasksInRange
            .OrderByDescending(task => task.CompletedAt ?? task.CreatedAt)
            .ThenBy(task => task.DueDate)
            .Select(task =>
            {
                var deadlineHealth = GetDeadlineHealth(
                    task.Status,
                    task.DueDate,
                    today);
                return new WorkspaceReportTask(
                    task.Id,
                    task.ProjectId,
                    projectLookup[task.ProjectId].Name,
                    task.AssignedUserId,
                    task.Title,
                    task.Status,
                    task.IsBlocked,
                    task.DueDate,
                    task.CreatedAt,
                    task.CompletedAt,
                    task.PriorityScore,
                    task.PriorityBand,
                    deadlineHealth,
                    task.Tags);
            })
            .ToArray();

        var reportProjects = projects
            .OrderBy(project => project.ArchivedAt is null ? 0 : 1)
            .ThenBy(project => project.DeliveryDate)
            .ThenBy(project => project.Name)
            .Select(project =>
            {
                var projectTasks = allTaskValues
                    .Where(task => task.ProjectId == project.Id)
                    .ToArray();
                var totalTasks = projectTasks.Length;
                var completedTasks = projectTasks.Count(
                    task => task.Status == TaskItemStatus.Completed);
                var completionPercentage = totalTasks == 0
                    ? 0
                    : (int)Math.Round(completedTasks * 100m / totalTasks);
                return new WorkspaceReportProject(
                    project.Id,
                    project.Name,
                    project.Description,
                    project.DeliveryDate,
                    project.ArchivedAt is not null,
                    project.ArchivedAt,
                    totalTasks,
                    completedTasks,
                    projectTasks.Count(task => task.Status != TaskItemStatus.Completed),
                    projectTasks.Count(task => task.IsBlocked),
                    projectTasks.Count(task =>
                        GetDeadlineHealth(
                            task.Status,
                            task.DueDate,
                            today) == DeadlineHealth.Overdue),
                    projectTasks.Count(task =>
                        task.PriorityBand == PriorityBand.Critical &&
                        task.Status != TaskItemStatus.Completed),
                    completionPercentage);
            })
            .ToArray();

        var notifications = (await BuildNotificationsAsync(
            projects,
            allTaskValues,
            today,
            currentUser.IsAuthenticated ? currentUser.UserId : null,
            cancellationToken))
            .ToArray();
        return new WorkspaceReportSnapshot(
            workspaceId,
            from,
            to,
            projects.Length,
            projects.Count(project => project.ArchivedAt is null),
            projects.Count(project => project.ArchivedAt is not null),
            projects.Count(project =>
                (project.DeliveryDate.HasValue &&
                 DateInRange(project.DeliveryDate.Value, from, to)) ||
                (project.ArchivedAt.HasValue &&
                 DateInRange(DateOnly.FromDateTime(project.ArchivedAt.Value.UtcDateTime), from, to))),
            tasksInRange.Length,
            tasksInRange.Count(task => task.Status == TaskItemStatus.Completed),
            tasksInRange.Count(task => task.Status != TaskItemStatus.Completed),
            tasksInRange.Count(task => task.IsBlocked),
            tasksInRange.Count(task =>
                task.PriorityBand == PriorityBand.Critical &&
                task.Status != TaskItemStatus.Completed),
            tasksInRange.Count(task =>
                GetDeadlineHealth(
                    task.Status,
                    task.DueDate,
                    today) == DeadlineHealth.Overdue),
            Enum.GetValues<TaskItemStatus>()
                .Select(status => new DashboardBreakdownItem(
                    status.ToString(),
                    tasksInRange.Count(task => task.Status == status)))
                .ToArray(),
            Enum.GetValues<PriorityBand>()
                .Select(band => new DashboardBreakdownItem(
                    band.ToString(),
                    tasksInRange.Count(task => task.PriorityBand == band)))
                .ToArray(),
            BuildDeadlineBreakdown(tasksInRange, today),
            reportProjects,
            reportTasks,
            notifications);
    }

    private static IReadOnlyList<DashboardBreakdownItem> BuildDeadlineBreakdown(
        IReadOnlyCollection<TaskReportValue> tasks,
        DateOnly today)
    {
        return
        [
            new DashboardBreakdownItem(
                "Overdue",
                tasks.Count(task => GetDeadlineHealth(
                    task.Status,
                    task.DueDate,
                    today) == DeadlineHealth.Overdue)),
            new DashboardBreakdownItem(
                "Due today",
                tasks.Count(task =>
                    task.Status != TaskItemStatus.Completed &&
                    task.DueDate == today)),
            new DashboardBreakdownItem(
                "Due in 7 days",
                tasks.Count(task =>
                    task.Status != TaskItemStatus.Completed &&
                    task.DueDate is not null &&
                    task.DueDate.Value > today &&
                    task.DueDate.Value <= today.AddDays(7))),
            new DashboardBreakdownItem(
                "Healthy",
                tasks.Count(task =>
                    task.Status != TaskItemStatus.Completed &&
                    (task.DueDate is null || task.DueDate.Value > today.AddDays(7))))
        ];
    }

    private async Task<IReadOnlyCollection<DashboardWarning>> BuildNotificationsAsync(
        IEnumerable<ProjectReportValue> projects,
        IEnumerable<TaskReportValue> tasks,
        DateOnly today,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var tomorrow = today.AddDays(1);
        var twoDaysFromNow = today.AddDays(2);
        var taskWarnings = tasks
            .Where(task => task.Status != TaskItemStatus.Completed)
            .Where(task =>
                task.DueDate is not null &&
                (task.DueDate.Value == today ||
                 task.DueDate.Value == tomorrow ||
                 task.DueDate.Value == twoDaysFromNow))
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
        var projectWarnings = projects
            .Where(project => project.ArchivedAt is null)
            .Where(project =>
                project.DeliveryDate is not null &&
                project.DeliveryDate.Value == tomorrow)
            .Select(project => new DashboardWarning(
                "ProjectDelivery",
                "warning",
                "Project delivery date reminder",
                $"{project.Name} reaches its delivery date in 24 hours.",
                ProjectId: project.Id,
                DueDate: project.DeliveryDate!.Value));
        var assignmentWarnings = tasks
            .Where(task =>
                currentUserId.HasValue &&
                task.AssignedUserId == currentUserId.Value &&
                task.Status != TaskItemStatus.Completed)
            .Select(task => new DashboardWarning(
                "TaskAssigned",
                "info",
                "Task assigned to you",
                $"{task.Title} is assigned to you.",
                ProjectId: task.ProjectId,
                TaskId: task.Id,
                DueDate: task.DueDate));

        var carryOverWarnings = await BuildPersonalTodoCarryOverWarningsAsync(
            today,
            currentUserId,
            cancellationToken);

        return taskWarnings
            .Concat(projectWarnings)
            .Concat(assignmentWarnings)
            .Concat(carryOverWarnings)
            .OrderBy(warning => warning.DueDate)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<DashboardWarning>> BuildPersonalTodoCarryOverWarningsAsync(
        DateOnly today,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue)
        {
            return [];
        }

        var carriedTodos = await context.PersonalTodos
            .AsNoTracking()
            .Where(todo =>
                todo.UserId == currentUserId.Value &&
                !todo.IsCompleted &&
                todo.TodoDate == today &&
                todo.CarriedOverFromDate != null)
            .Select(todo => new
            {
                todo.Title,
                todo.CarriedOverFromDate
            })
            .OrderBy(todo => todo.CarriedOverFromDate)
            .ThenBy(todo => todo.Title)
            .ToArrayAsync(cancellationToken);

        if (carriedTodos.Length == 0)
        {
            return [];
        }

        var sampleTitles = string.Join(
            ", ",
            carriedTodos.Take(3).Select(todo => todo.Title));
        var moreCount = carriedTodos.Length > 3
            ? $", plus {carriedTodos.Length - 3} more"
            : string.Empty;
        var oldestDate = carriedTodos
            .Select(todo => todo.CarriedOverFromDate)
            .FirstOrDefault();

        return
        [
            new DashboardWarning(
                "PersonalTodoCarryOver",
                "warning",
                $"{carriedTodos.Length} My Day todo{(carriedTodos.Length == 1 ? string.Empty : "s")} carried over",
                $"{sampleTitles}{moreCount} moved into today from {oldestDate:yyyy-MM-dd}.",
                DueDate: today)
        ];
    }

    private static bool IsTaskInRange(
        DateTimeOffset createdAt,
        DateOnly? dueDate,
        DateTimeOffset? completedAt,
        DateOnly? from,
        DateOnly? to) =>
        DateInRange(DateOnly.FromDateTime(createdAt.UtcDateTime), from, to) ||
        (dueDate.HasValue && DateInRange(dueDate.Value, from, to)) ||
        (completedAt.HasValue &&
         DateInRange(DateOnly.FromDateTime(completedAt.Value.UtcDateTime), from, to));

    private static bool DateInRange(DateOnly value, DateOnly? from, DateOnly? to) =>
        (!from.HasValue || value >= from.Value) &&
        (!to.HasValue || value <= to.Value);

    private static DeadlineHealth GetDeadlineHealth(
        TaskItemStatus status,
        DateOnly? dueDate,
        DateOnly today)
    {
        if (status == TaskItemStatus.Completed)
        {
            return DeadlineHealth.Completed;
        }

        if (!dueDate.HasValue)
        {
            return DeadlineHealth.Healthy;
        }

        var daysRemaining = dueDate.Value.DayNumber - today.DayNumber;
        return daysRemaining switch
        {
            < 0 => DeadlineHealth.Overdue,
            <= 3 => DeadlineHealth.AtRisk,
            _ => DeadlineHealth.Healthy
        };
    }

    private sealed record ProjectReportValue(
        Guid Id,
        string Name,
        string? Description,
        DateOnly? DeliveryDate,
        DateTimeOffset? ArchivedAt);

    private sealed record TaskReportValue(
        Guid Id,
        Guid ProjectId,
        Guid? AssignedUserId,
        string Title,
        TaskItemStatus Status,
        DateOnly? DueDate,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt,
        bool IsBlocked,
        decimal? PriorityScore,
        PriorityBand? PriorityBand,
        IReadOnlyCollection<string> Tags);
}
