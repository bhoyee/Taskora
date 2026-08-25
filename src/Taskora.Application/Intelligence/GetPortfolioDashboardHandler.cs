using TodoApp.Application.Abstractions;

namespace TodoApp.Application.Intelligence;

/// <summary>
/// Query for a portfolio dashboard snapshot, optionally scoped to a specific
/// workspace and/or project. When both are omitted, the snapshot spans the
/// entire portfolio visible to the caller.
/// </summary>
public sealed record GetPortfolioDashboardQuery(
    Guid? WorkspaceId = null,
    Guid? ProjectId = null);

/// <summary>Builds aggregated portfolio dashboard snapshots for reporting.</summary>
public sealed class GetPortfolioDashboardHandler(
    IPortfolioDashboardReadRepository dashboard)
{
    /// <summary>
    /// Delegates to the read repository to compute the portfolio dashboard
    /// snapshot for the given optional workspace/project scope. This is a
    /// read-only reporting query with no authorization checks of its own.
    /// </summary>
    public Task<PortfolioDashboardSnapshot> HandleAsync(
        GetPortfolioDashboardQuery query,
        CancellationToken cancellationToken) =>
        dashboard.GetAsync(
            query.WorkspaceId,
            query.ProjectId,
            cancellationToken);
}
