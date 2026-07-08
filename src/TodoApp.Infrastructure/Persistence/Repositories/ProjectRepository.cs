using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Projects;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository(TodoAppDbContext context)
    : IProjectRepository
{
    public async Task AddAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        await context.Projects.AddAsync(project, cancellationToken);
    }

    public Task<Project?> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        context.Projects
            .Include("_categories")
            .SingleOrDefaultAsync(
            project => project.Id == projectId,
            cancellationToken);

    public async Task<IReadOnlyList<Project>> ListForWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken) =>
        await context.Projects
            .AsNoTracking()
            .Include("_categories")
            .Where(project => project.WorkspaceId == workspaceId)
            .OrderBy(project => project.Name)
            .ToArrayAsync(cancellationToken);
}
