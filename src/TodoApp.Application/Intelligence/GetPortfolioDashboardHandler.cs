using TodoApp.Application.Abstractions;

namespace TodoApp.Application.Intelligence;

public sealed record GetPortfolioDashboardQuery;

public sealed class GetPortfolioDashboardHandler(
    IPortfolioDashboardReadRepository dashboard)
{
    public Task<PortfolioDashboardSnapshot> HandleAsync(
        GetPortfolioDashboardQuery query,
        CancellationToken cancellationToken) =>
        dashboard.GetAsync(cancellationToken);
}
