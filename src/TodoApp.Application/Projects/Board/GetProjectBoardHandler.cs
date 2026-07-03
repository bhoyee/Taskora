using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;

namespace TodoApp.Application.Projects.Board;

public sealed class GetProjectBoardHandler(
    IProjectRepository projects,
    IProjectBoardReadRepository boardReader)
{
    public async Task<Result<ProjectBoardDto>> HandleAsync(
        GetProjectBoardQuery query,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(
            query.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return Result<ProjectBoardDto>.Failure(
                new ApplicationError(
                    "project.not_found",
                    "The project was not found.",
                    ErrorType.NotFound));
        }

        var snapshot = await boardReader.GetAsync(
            project.Id,
            cancellationToken);
        var blockedTasks = snapshot.HighPriorityBlockedTasks
            .Select(task =>
                new HighPriorityBlockedTaskDto(
                    task.Id,
                    task.Title,
                    task.Priority.Value,
                    task.DependencyIds))
            .ToArray();

        return Result<ProjectBoardDto>.Success(
            new ProjectBoardDto(
                project.Id,
                project.Name,
                snapshot.BacklogCount,
                snapshot.ReadyCount,
                snapshot.InProgressCount,
                snapshot.BlockedCount,
                snapshot.CompletedCount,
                snapshot.OverdueCount,
                blockedTasks));
    }
}
