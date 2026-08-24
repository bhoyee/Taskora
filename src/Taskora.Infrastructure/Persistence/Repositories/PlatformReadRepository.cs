using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class PlatformReadRepository(TodoAppDbContext context)
    : IPlatformReadRepository
{
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
