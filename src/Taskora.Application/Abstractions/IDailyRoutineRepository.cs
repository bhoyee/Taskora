using TodoApp.Domain.Todos;

namespace TodoApp.Application.Abstractions;

public sealed record DailyRoutineSearchResult(
    IReadOnlyList<DailyRoutine> Items,
    int TotalCount);

public interface IDailyRoutineRepository
{
    Task AddAsync(
        DailyRoutine routine,
        CancellationToken cancellationToken);

    Task<DailyRoutine?> GetByIdAsync(
        Guid routineId,
        CancellationToken cancellationToken);

    Task<DailyRoutineSearchResult> SearchAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyRoutine>> ListDueForGenerationAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken);

    Task<bool> GeneratedTodoExistsAsync(
        Guid routineId,
        DateOnly businessDate,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        DailyRoutine routine,
        CancellationToken cancellationToken);
}
