using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Projects;

namespace TodoApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for the <see cref="Project"/> aggregate, covering creation,
/// tracked lookup by id, cascading deletion, and workspace listings.
/// </summary>
public sealed class ProjectRepository(TodoAppDbContext context)
    : IProjectRepository
{
    /// <summary>Stages a new project for insertion; persistence happens on unit-of-work save.</summary>
    public async Task AddAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        await context.Projects.AddAsync(project, cancellationToken);
    }

    /// <summary>
    /// Loads a tracked project by id for mutation. Explicitly includes the
    /// <c>_categories</c> and <c>_sprints</c> backing collections by shadow
    /// name because they are not owned navigations and would otherwise stay
    /// empty/unloaded on the returned aggregate.
    /// </summary>
    public Task<Project?> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        context.Projects
            .Include("_categories")
            .Include("_sprints")
            .SingleOrDefaultAsync(
            project => project.Id == projectId,
            cancellationToken);

    /// <summary>
    /// Deletes a project along with its tasks, first removing dependency
    /// rows that reference those tasks. <c>TaskDependencies</c> rows aren't
    /// modeled as an EF-owned relationship reachable from
    /// <see cref="Project"/>, so they must be cleaned up with raw SQL before
    /// the tasks can be removed (FK constraint). The raw SQL is duplicated
    /// with quoted identifiers for Postgres/Npgsql (case-sensitive
    /// identifiers) and unquoted identifiers for SQLite, selected at runtime
    /// via the active provider name.
    /// </summary>
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

    /// <summary>
    /// Lists all projects in a workspace, ordered by name. Read-only
    /// (<c>AsNoTracking()</c>) but still explicitly includes the
    /// <c>_categories</c> and <c>_sprints</c> shadow collections so callers
    /// can read those without triggering per-project lazy queries.
    /// </summary>
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
