using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Abstractions;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken);

    Task AddAsync(TaskItem task, CancellationToken cancellationToken);

    Task RemoveAsync(TaskItem task, CancellationToken cancellationToken);
}
