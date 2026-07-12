using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Abstractions;

public interface IWorkspaceReportReadRepository
{
    Task<WorkspaceReportSnapshot> GetAsync(
        Guid workspaceId,
        DateOnly? from,
        DateOnly? to,
        Guid? projectId,
        CancellationToken cancellationToken);
}

public sealed record WorkspaceReportSnapshot(
    Guid WorkspaceId,
    DateOnly? From,
    DateOnly? To,
    int TotalProjects,
    int ActiveProjects,
    int ArchivedProjects,
    int ProjectsDeliveredInRange,
    int TotalTasks,
    int CompletedTasks,
    int ActiveTasks,
    int BlockedTasks,
    int CriticalTasks,
    int OverdueTasks,
    IReadOnlyList<DashboardBreakdownItem> StatusBreakdown,
    IReadOnlyList<DashboardBreakdownItem> PriorityBreakdown,
    IReadOnlyList<DashboardBreakdownItem> DeadlineBreakdown,
    IReadOnlyList<WorkspaceReportProject> Projects,
    IReadOnlyList<WorkspaceReportTask> Tasks,
    IReadOnlyList<DashboardWarning> Notifications);

public sealed record WorkspaceReportProject(
    Guid Id,
    string Name,
    string? Description,
    DateOnly? DeliveryDate,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    int TotalTasks,
    int CompletedTasks,
    int ActiveTasks,
    int BlockedTasks,
    int OverdueTasks,
    int CriticalTasks,
    int CompletionPercentage);

public sealed record WorkspaceReportTask(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid? AssignedUserId,
    string Title,
    TaskItemStatus Status,
    bool IsBlocked,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    decimal? PriorityScore,
    PriorityBand? PriorityBand,
    DeadlineHealth DeadlineHealth,
    IReadOnlyCollection<string> Tags);
