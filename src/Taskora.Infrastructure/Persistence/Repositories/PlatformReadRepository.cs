using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only repository backing the platform admin views: cross-workspace
/// summaries and per-project task counts, aggregated across the whole
/// database rather than scoped to a single workspace or project.
/// </summary>
public sealed class PlatformReadRepository(TodoAppDbContext context)
    : IPlatformReadRepository
{
    /// <summary>
    /// Builds a summary row per workspace (owner info, role counts, project/
    /// sprint/task counts) for the platform admin dashboard. Runs several
    /// independent aggregate queries (owners by id, membership role counts
    /// grouped by workspace+role, and project/sprint/task counts grouped by
    /// workspace) and stitches them together client-side by dictionary
    /// lookup, rather than a single large join, to keep each grouped
    /// aggregate query simple and independently cacheable. Sprint and task
    /// counts are computed via an explicit <c>Join</c> to <c>Projects</c>
    /// because neither <c>Sprint</c> nor <c>TaskItem</c> carries a direct
    /// workspace id. All queries use <c>AsNoTracking()</c> since this is a
    /// pure reporting read.
    /// </summary>
    public async Task<IReadOnlyList<PlatformWorkspaceSummary>> ListWorkspaceSummariesAsync(
        CancellationToken cancellationToken)
    {
        var workspaces = await context.Workspaces
            .AsNoTracking()
            .OrderBy(workspace => workspace.Name)
            .Select(workspace => new
            {
                workspace.Id,
                workspace.Name,
                workspace.OwnerId,
                workspace.SuspendedAt,
            })
            .ToArrayAsync(cancellationToken);

        var ownerIds = workspaces.Select(workspace => workspace.OwnerId).ToArray();
        var owners = await context.UserProfiles
            .AsNoTracking()
            .Where(user => ownerIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var roleCounts = await context.WorkspaceMemberships
            .AsNoTracking()
            .Where(membership => membership.Role != WorkspaceRole.Owner)
            .GroupBy(membership => new { membership.WorkspaceId, membership.Role })
            .Select(group => new
            {
                group.Key.WorkspaceId,
                group.Key.Role,
                Count = group.Count(),
            })
            .ToArrayAsync(cancellationToken);

        var projectCounts = await context.Projects
            .AsNoTracking()
            .GroupBy(project => project.WorkspaceId)
            .Select(group => new { WorkspaceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(
                entry => entry.WorkspaceId,
                entry => entry.Count,
                cancellationToken);

        var sprintCounts = await context.Sprints
            .AsNoTracking()
            .Join(
                context.Projects,
                sprint => sprint.ProjectId,
                project => project.Id,
                (sprint, project) => project.WorkspaceId)
            .GroupBy(workspaceId => workspaceId)
            .Select(group => new { WorkspaceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(
                entry => entry.WorkspaceId,
                entry => entry.Count,
                cancellationToken);

        var taskCounts = await context.Tasks
            .AsNoTracking()
            .Join(
                context.Projects,
                task => task.ProjectId,
                project => project.Id,
                (task, project) => project.WorkspaceId)
            .GroupBy(workspaceId => workspaceId)
            .Select(group => new { WorkspaceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(
                entry => entry.WorkspaceId,
                entry => entry.Count,
                cancellationToken);

        return workspaces.Select(workspace =>
        {
            owners.TryGetValue(workspace.OwnerId, out var owner);
            var managerCount = roleCounts
                .FirstOrDefault(entry =>
                    entry.WorkspaceId == workspace.Id &&
                    entry.Role == WorkspaceRole.Manager)
                ?.Count ?? 0;
            var memberCount = roleCounts
                .FirstOrDefault(entry =>
                    entry.WorkspaceId == workspace.Id &&
                    entry.Role == WorkspaceRole.Member)
                ?.Count ?? 0;

            return new PlatformWorkspaceSummary(
                workspace.Id,
                workspace.Name,
                workspace.OwnerId,
                owner?.DisplayName ?? "Unknown",
                owner?.Email ?? string.Empty,
                workspace.SuspendedAt.HasValue,
                workspace.SuspendedAt,
                managerCount,
                memberCount,
                projectCounts.GetValueOrDefault(workspace.Id),
                sprintCounts.GetValueOrDefault(workspace.Id),
                taskCounts.GetValueOrDefault(workspace.Id));
        }).ToArray();
    }

    /// <summary>
    /// Returns a task-count-per-project dictionary for the given project ids
    /// via a grouped <c>AsNoTracking()</c> query; returns an empty dictionary
    /// without querying if no project ids are supplied.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, int>> GetTaskCountsByProjectAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var counts = await context.Tasks
            .AsNoTracking()
            .Where(task => projectIds.Contains(task.ProjectId))
            .GroupBy(task => task.ProjectId)
            .Select(group => new { ProjectId = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);

        return counts.ToDictionary(entry => entry.ProjectId, entry => entry.Count);
    }
}
