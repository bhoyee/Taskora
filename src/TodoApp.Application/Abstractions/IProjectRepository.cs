using TodoApp.Domain.Projects;

namespace TodoApp.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken);
}
