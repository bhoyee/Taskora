using TodoApp.Application.Abstractions;
using TodoApp.Application.Collaboration;
using TodoApp.Application.Common;

namespace TodoApp.Application.Platform;

/// <summary>Query to list summaries of every workspace on the platform, for platform-administration views.</summary>
public sealed record ListPlatformWorkspacesQuery;

/// <summary>Query for the detailed platform-administration view of a single workspace.</summary>
public sealed record GetPlatformWorkspaceDetailQuery(Guid WorkspaceId);

/// <summary>Summarizes a project's size for the platform workspace detail view.</summary>
public sealed record PlatformProjectSummaryDto(
    Guid ProjectId,
    string ProjectName,
    bool IsArchived,
    int SprintCount,
    int TaskCount);

/// <summary>Detailed platform-administration view of a workspace: its members, projects, and dashboard.</summary>
public sealed record PlatformWorkspaceDetailDto(
    Guid WorkspaceId,
    string WorkspaceName,
    bool IsSuspended,
    DateTimeOffset? SuspendedAt,
    string? SuspendedReason,
    IReadOnlyList<WorkspaceMemberDto> Members,
    IReadOnlyList<PlatformProjectSummaryDto> Projects,
    PortfolioDashboardSnapshot Dashboard);

/// <summary>Lists summaries of every workspace on the platform, for platform-administration views.</summary>
public sealed class ListPlatformWorkspacesHandler(IPlatformReadRepository platform)
{
    /// <summary>
    /// Returns a summary of every workspace on the platform. This is an
    /// administrative read with no per-workspace membership checks; access
    /// control is expected to be enforced by the caller (e.g. a
    /// platform-admin-only endpoint).
    /// </summary>
    public async Task<Result<IReadOnlyList<PlatformWorkspaceSummary>>> HandleAsync(
        ListPlatformWorkspacesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await platform.ListWorkspaceSummariesAsync(cancellationToken);
        return Result<IReadOnlyList<PlatformWorkspaceSummary>>.Success(result);
    }
}

/// <summary>Builds the detailed platform-administration view of a single workspace.</summary>
public sealed class GetPlatformWorkspaceDetailHandler(
    IWorkspaceRepository workspaces,
    IProjectRepository projects,
    IPlatformReadRepository platform,
    IPortfolioDashboardReadRepository dashboard,
    GetWorkspaceMembersHandler members)
{
    /// <summary>
    /// Requires the workspace to exist, then assembles its member list (via
    /// <see cref="GetWorkspaceMembersHandler"/> with the administrative
    /// bypass, since this is a platform-admin view), its projects with task
    /// counts, and its portfolio dashboard snapshot into a single
    /// <see cref="PlatformWorkspaceDetailDto"/>.
    /// </summary>
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
