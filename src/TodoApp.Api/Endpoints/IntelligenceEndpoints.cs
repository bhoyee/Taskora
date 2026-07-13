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
                    Guid? workspaceId,
                    Guid? projectId,
                    GetPortfolioDashboardHandler handler,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await handler.HandleAsync(
                        new GetPortfolioDashboardQuery(workspaceId, projectId),
                        cancellationToken)))
            .WithTags("Intelligence")
            .RequireAuthorization()
            .WithName("GetPortfolioDashboard");

        endpoints.MapGet(
                "/api/v1/workspaces/{workspaceId:guid}/reports",
                async (
                    Guid workspaceId,
                    DateOnly? from,
                    DateOnly? to,
                    Guid? projectId,
                    GetWorkspaceReportHandler handler,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await handler.HandleAsync(
                        new GetWorkspaceReportQuery(
                            workspaceId,
                            from,
                            to,
                            projectId),
                        cancellationToken)))
            .WithTags("Intelligence")
            .RequireAuthorization()
            .WithName("GetWorkspaceReport");

        return endpoints;
    }
}
