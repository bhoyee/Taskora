using TodoApp.Application.Abstractions;
using TodoApp.Application.Collaboration;
using TodoApp.Application.Common;

namespace TodoApp.Application.Platform;

public sealed record ListPlatformWorkspacesQuery;

public sealed record GetPlatformWorkspaceDetailQuery(Guid WorkspaceId);

public sealed record PlatformProjectSummaryDto(
    Guid ProjectId,
    string ProjectName,
    bool IsArchived,
    int SprintCount,
    int TaskCount);

public sealed record PlatformWorkspaceDetailDto(
    Guid WorkspaceId,
    string WorkspaceName,
    bool IsSuspended,
    DateTimeOffset? SuspendedAt,
    string? SuspendedReason,
    IReadOnlyList<WorkspaceMemberDto> Members,
    IReadOnlyList<PlatformProjectSummaryDto> Projects,
    PortfolioDashboardSnapshot Dashboard);

public sealed class ListPlatformWorkspacesHandler(IPlatformReadRepository platform)
{
    public async Task<Result<IReadOnlyList<PlatformWorkspaceSummary>>> HandleAsync(
        ListPlatformWorkspacesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await platform.ListWorkspaceSummariesAsync(cancellationToken);
        return Result<IReadOnlyList<PlatformWorkspaceSummary>>.Success(result);
    }
}

public sealed class GetPlatformWorkspaceDetailHandler(
    IWorkspaceRepository workspaces,
    IProjectRepository projects,
    IPlatformReadRepository platform,
    IPortfolioDashboardReadRepository dashboard,
    GetWorkspaceMembersHandler members)
{
    public async Task<Result<PlatformWorkspaceDetailDto>> HandleAsync(
        GetPlatformWorkspaceDetailQuery query,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaces.GetByIdAsync(
            query.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            return Result<PlatformWorkspaceDetailDto>.Failure(
                new ApplicationError(
                    "workspace.not_found",
                    "The workspace was not found.",
                    ErrorType.NotFound));
        }

        var memberResult = await members.HandleAsync(
            new GetWorkspaceMembersQuery(
                query.WorkspaceId,
                HasAdministrativeBypass: true),
            cancellationToken);
        if (!memberResult.IsSuccess)
        {
            return Result<PlatformWorkspaceDetailDto>.Failure(memberResult.Error);
        }

        var projectList = await projects.ListForWorkspaceAsync(
            query.WorkspaceId, cancellationToken);
        var taskCounts = await platform.GetTaskCountsByProjectAsync(
            projectList.Select(project => project.Id).ToArray(),
            cancellationToken);

        var projectSummaries = projectList
            .Select(project => new PlatformProjectSummaryDto(
                project.Id,
                project.Name,
                project.IsArchived,
                project.Sprints.Count,
                taskCounts.GetValueOrDefault(project.Id)))
            .ToArray();

        var dashboardSnapshot = await dashboard.GetAsync(
            query.WorkspaceId, null, cancellationToken);

        return Result<PlatformWorkspaceDetailDto>.Success(
            new PlatformWorkspaceDetailDto(
                workspace.Id,
                workspace.Name,
                workspace.IsSuspended,
                workspace.SuspendedAt,
                workspace.SuspendedReason,
                memberResult.Value,
                projectSummaries,
                dashboardSnapshot));
    }
}
