using TodoApp.Application.Intelligence;

namespace TodoApp.Api.Endpoints;

internal static class IntelligenceEndpoints
{
    public static IEndpointRouteBuilder MapIntelligenceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/dashboard",
                async (
                    GetPortfolioDashboardHandler handler,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await handler.HandleAsync(
                        new GetPortfolioDashboardQuery(),
                        cancellationToken)))
            .WithTags("Intelligence")
            .RequireAuthorization()
            .WithName("GetPortfolioDashboard");

        return endpoints;
    }
}
