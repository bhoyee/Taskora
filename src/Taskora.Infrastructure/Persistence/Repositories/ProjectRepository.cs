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
            .Include("_sprints")
            .SingleOrDefaultAsync(
            project => project.Id == projectId,
            cancellationToken);

    public async Task RemoveAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        if (context.Database.ProviderName?.Contains(
                "Npgsql",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                DELETE FROM "TaskDependencies"
                WHERE "TaskId" IN (
                    SELECT "Id" FROM "Tasks" WHERE "ProjectId" = {project.Id}
                )
                OR "DependencyId" IN (
                    SELECT "Id" FROM "Tasks" WHERE "ProjectId" = {project.Id}
                )
                """,
                cancellationToken);
        }
        else
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                DELETE FROM TaskDependencies
                WHERE TaskId IN (
                    SELECT Id FROM Tasks WHERE ProjectId = {project.Id}
                )
                OR DependencyId IN (
                    SELECT Id FROM Tasks WHERE ProjectId = {project.Id}
                )
                """,
                cancellationToken);
        }

        var projectTasks = await context.Tasks
            .Where(task => task.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);

        context.Tasks.RemoveRange(projectTasks);
        context.Projects.Remove(project);
    }

    public async Task<IReadOnlyList<Project>> ListForWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken) =>
        await context.Projects
            .AsNoTracking()
            .Include("_categories")
            .Include("_sprints")
            .Where(project => project.WorkspaceId == workspaceId)
            .OrderBy(project => project.Name)
            .ToArrayAsync(cancellationToken);
}
