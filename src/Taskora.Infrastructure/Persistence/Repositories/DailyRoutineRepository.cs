using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for the <see cref="DailyRoutine"/> aggregate, which represents
/// a recurring template used to auto-generate <see cref="TodoApp.Domain.Todos.PersonalTodo"/>
/// entries on each due business date.
/// </summary>
public sealed class DailyRoutineRepository(TodoAppDbContext context)
    : IDailyRoutineRepository
{
    /// <summary>Stages a new routine for insertion; persistence happens on unit-of-work save.</summary>
    public async Task AddAsync(
        DailyRoutine routine,
        CancellationToken cancellationToken)
    {
        await context.DailyRoutines.AddAsync(routine, cancellationToken);
    }

    /// <summary>Loads a tracked routine by id for mutation.</summary>
    public Task<DailyRoutine?> GetByIdAsync(
        Guid routineId,
        CancellationToken cancellationToken) =>
        context.DailyRoutines
            .FirstOrDefaultAsync(
                routine => routine.Id == routineId,
                cancellationToken);

    /// <summary>
    /// Returns a paged list of a user's routines (active routines first, then
    /// alphabetically) along with the total match count, using
    /// <c>AsNoTracking()</c> for the read-only listing.
    /// </summary>
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

    /// <summary>
    /// Finds active routines that are within their start/end window for the
    /// given business date and have not yet generated a todo for that date
    /// (tracked via <c>LastGeneratedDate</c>), used by the daily-generation
    /// background job to decide which routines still need processing.
    /// </summary>
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

    /// <summary>
    /// Checks whether a personal todo has already been generated from this
    /// routine for the given business date, used as an idempotency guard so
    /// the generation job doesn't create duplicate todos.
    /// </summary>
    public Task<bool> GeneratedTodoExistsAsync(
        Guid routineId,
        DateOnly businessDate,
        CancellationToken cancellationToken) =>
        context.PersonalTodos.AnyAsync(
            todo =>
                todo.DailyRoutineId == routineId &&
                todo.TodoDate == businessDate,
            cancellationToken);

    /// <summary>Stages a routine for deletion; persistence happens on unit-of-work save.</summary>
    public Task RemoveAsync(
        DailyRoutine routine,
        CancellationToken cancellationToken)
    {
        context.DailyRoutines.Remove(routine);
        return Task.CompletedTask;
    }
}
