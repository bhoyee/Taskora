using TodoApp.Domain.Projects;

namespace TodoApp.Application.Abstractions;

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken cancellationToken);

    Task<Project?> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken);
}
