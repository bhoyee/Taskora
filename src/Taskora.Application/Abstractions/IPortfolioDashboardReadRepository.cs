namespace TodoApp.Application.Abstractions;

/// <summary>
/// Read-only repository that aggregates task and project data into a single
/// portfolio dashboard snapshot for reporting purposes.
/// </summary>
public interface IPortfolioDashboardReadRepository
{
    /// <summary>
    /// Builds a portfolio dashboard snapshot, optionally scoped to a specific workspace and/or project.
    /// </summary>
    /// <param name="workspaceId">The workspace to scope the snapshot to, or null to include all workspaces.</param>
    /// <param name="projectId">The project to scope the snapshot to, or null to include all projects.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The aggregated portfolio dashboard snapshot.</returns>
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
