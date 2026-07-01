using TodoApp.Application.Tasks.Queries;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Abstractions;

public interface ITaskReadRepository
{
    Task<TaskItem?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken);

    Task<TaskSearchResult> SearchAsync(
        TaskSearchCriteria criteria,
        CancellationToken cancellationToken);
}
