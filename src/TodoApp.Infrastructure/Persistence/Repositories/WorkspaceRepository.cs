using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class WorkspaceRepository(TodoAppDbContext context)
    : IWorkspaceRepository
{
    public Task<Workspace?> GetByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken) =>
        context.Workspaces
            .Include("_memberships")
            .SingleOrDefaultAsync(
                workspace => workspace.Id == workspaceId,
                cancellationToken);

    public async Task<IReadOnlyList<Workspace>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await context.Workspaces
            .AsNoTracking()
            .Include("_memberships")
            .Where(workspace =>
                context.WorkspaceMemberships.Any(membership =>
                    membership.WorkspaceId == workspace.Id &&
                    membership.UserId == userId))
            .OrderBy(workspace => workspace.Name)
            .ToArrayAsync(cancellationToken);
}

public sealed class UserProfileRepository(TodoAppDbContext context)
    : IUserProfileRepository
{
    public Task<UserProfile?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken) =>
        context.UserProfiles.SingleOrDefaultAsync(
            user => user.Email == email,
            cancellationToken);

    public async Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        await context.UserProfiles
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToArrayAsync(cancellationToken);
}
