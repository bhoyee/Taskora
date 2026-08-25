using TodoApp.Domain.Todos;

namespace TodoApp.Application.Abstractions;

public sealed record DailyRoutineSearchResult(
    IReadOnlyList<DailyRoutine> Items,
    int TotalCount);

/// <summary>
/// Repository abstraction for persisting and retrieving <see cref="DailyRoutine"/> entities,
/// which describe recurring personal to-do templates and their generation schedule.
/// </summary>
public interface IDailyRoutineRepository
{
    /// <summary>
    /// Registers a new daily routine to be inserted when changes are persisted.
    /// </summary>
    /// <param name="routine">The routine to add.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task AddAsync(
        DailyRoutine routine,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the daily routine with the given identifier.
    /// </summary>
    /// <param name="routineId">The identifier of the routine to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching routine, or null if no routine with that identifier exists.</returns>
    Task<DailyRoutine?> GetByIdAsync(
        Guid routineId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a page of daily routines belonging to the given user.
    /// </summary>
    /// <param name="userId">The identifier of the user whose routines are being retrieved.</param>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The maximum number of routines to return per page.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The requested page of routines along with the total matching count.</returns>
    Task<DailyRoutineSearchResult> SearchAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the routines that are due to generate a to-do for the given business date.
    /// </summary>
    /// <param name="businessDate">The business date to check routines against.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The routines due for generation on the given date.</returns>
    Task<IReadOnlyList<DailyRoutine>> ListDueForGenerationAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether a to-do has already been generated for the given routine and business date.
    /// </summary>
    /// <param name="routineId">The identifier of the routine to check.</param>
    /// <param name="businessDate">The business date to check for an existing generated to-do.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>True if a to-do has already been generated for that routine and date; otherwise, false.</returns>
    Task<bool> GeneratedTodoExistsAsync(
        Guid routineId,
        DateOnly businessDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Registers an existing daily routine to be removed when changes are persisted.
    /// </summary>
    /// <param name="routine">The routine to remove.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task RemoveAsync(
        DailyRoutine routine,
        CancellationToken cancellationToken);
}
