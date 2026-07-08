using TodoApp.Application.Abstractions;

namespace TodoApp.Application.Intelligence;

public sealed record GetPortfolioDashboardQuery(Guid? ProjectId = null);

public sealed class GetPortfolioDashboardHandler(
    IPortfolioDashboardReadRepository dashboard)
{
    public Task<PortfolioDashboardSnapshot> HandleAsync(
        GetPortfolioDashboardQuery query,
        CancellationToken cancellationToken) =>
        dashboard.GetAsync(query.ProjectId, cancellationToken);
}
