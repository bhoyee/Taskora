using TodoApp.Application.Intelligence;

namespace TodoApp.Api.Endpoints;

/// <summary>
/// Registers read-only "Intelligence" endpoints that aggregate data across
/// projects/tasks into dashboard and reporting views.
/// </summary>
internal static class IntelligenceEndpoints
{
    /// <summary>
    /// Maps the portfolio dashboard and workspace report endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapIntelligenceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        // GET /api/v1/dashboard: portfolio-wide (or workspace/project-scoped, via
        // optional query params) summary metrics for the current user.
        // Auth: any authenticated user (RequireAuthorization; scope is limited to
        // data the handler resolves as visible to the caller).
        // Returns: 200 with the dashboard DTO.
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

        // GET /api/v1/workspaces/{workspaceId}/reports: report data for a
        // workspace, optionally filtered by date range and project.
        // Auth: any authenticated user (RequireAuthorization; membership/authorization
        // enforced by the handler).
        // Returns: 200 with the report DTO.
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
