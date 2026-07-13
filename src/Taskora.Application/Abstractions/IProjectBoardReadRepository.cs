using TodoApp.Application.Projects.Board;

namespace TodoApp.Application.Abstractions;

public interface IProjectBoardReadRepository
{
    Task<ProjectBoardSnapshot> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken);
}
