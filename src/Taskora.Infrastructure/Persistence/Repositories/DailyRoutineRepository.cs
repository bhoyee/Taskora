using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class DailyRoutineRepository(TodoAppDbContext context)
    : IDailyRoutineRepository
{
    public async Task AddAsync(
        DailyRoutine routine,
        CancellationToken cancellationToken)
    {
        await context.DailyRoutines.AddAsync(routine, cancellationToken);
    }

    public Task<DailyRoutine?> GetByIdAsync(
        Guid routineId,
        CancellationToken cancellationToken) =>
        context.DailyRoutines
            .FirstOrDefaultAsync(
                routine => routine.Id == routineId,
                cancellationToken);

    public async Task<DailyRoutineSearchResult> SearchAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = context.DailyRoutines
            .AsNoTracking()
            .Where(routine => routine.UserId == userId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(routine => routine.IsActive)
            .ThenBy(routine => routine.Title)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new DailyRoutineSearchResult(items, totalCount);
    }

    public async Task<IReadOnlyList<DailyRoutine>> ListDueForGenerationAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken) =>
        await context.DailyRoutines
            .Where(routine =>
                routine.IsActive &&
                routine.StartDate <= businessDate &&
                (routine.EndDate == null || routine.EndDate >= businessDate) &&
                routine.LastGeneratedDate != businessDate)
            .OrderBy(routine => routine.UserId)
            .ThenBy(routine => routine.Title)
            .ToArrayAsync(cancellationToken);

    public Task<bool> GeneratedTodoExistsAsync(
        Guid routineId,
        DateOnly businessDate,
        CancellationToken cancellationToken) =>
        context.PersonalTodos.AnyAsync(
            todo =>
                todo.DailyRoutineId == routineId &&
                todo.TodoDate == businessDate,
            cancellationToken);

    public Task RemoveAsync(
        DailyRoutine routine,
        CancellationToken cancellationToken)
    {
        context.DailyRoutines.Remove(routine);
        return Task.CompletedTask;
    }
}
