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
    IReadOnlyList<DashboardWarning> Warnings);

public sealed record DashboardWarning(
    string Type,
    string Severity,
    string Title,
    string Message,
    Guid? ProjectId = null,
    Guid? TaskId = null,
    DateOnly? DueDate = null);
