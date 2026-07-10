namespace TodoApp.Application.Abstractions;

public interface IPortfolioDashboardReadRepository
{
    Task<PortfolioDashboardSnapshot> GetAsync(
        Guid? workspaceId,
        Guid? projectId,
        CancellationToken cancellationToken);
}

public sealed record PortfolioDashboardSnapshot(
    int ProjectCount,
    int ActiveTaskCount,
    int BlockedTaskCount,
    int OverdueTaskCount,
    int CriticalTaskCount,
    IReadOnlyList<DashboardBreakdownItem> StatusBreakdown,
    IReadOnlyList<DashboardBreakdownItem> PriorityBreakdown,
    IReadOnlyList<DashboardBreakdownItem> DeadlineBreakdown,
    DashboardProjectProgress ProjectProgress,
    IReadOnlyList<DashboardWarning> Warnings);

public sealed record DashboardBreakdownItem(string Label, int Count);

public sealed record DashboardProjectProgress(
    int CompletedTasks,
    int TotalTasks,
    int CompletionPercentage);

public sealed record DashboardWarning(
    string Type,
    string Severity,
    string Title,
    string Message,
    Guid? ProjectId = null,
    Guid? TaskId = null,
    DateOnly? DueDate = null);
