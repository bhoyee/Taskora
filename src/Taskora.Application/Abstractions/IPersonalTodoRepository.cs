using TodoApp.Domain.Todos;

namespace TodoApp.Application.Abstractions;

public sealed record PersonalTodoSearchCriteria(
    Guid UserId,
    DateOnly? Date,
    string? Search,
    int PageNumber,
    int PageSize);

public sealed record PersonalTodoSearchResult(
    IReadOnlyList<PersonalTodo> Items,
    int TotalCount);

public sealed record PersonalTodoOwner(
    Guid UserId,
    string DisplayName,
    string Email);

/// <summary>
/// Repository abstraction for persisting and retrieving a user's <see cref="PersonalTodo"/> items.
/// </summary>
public interface IPersonalTodoRepository
{
    /// <summary>
    /// Registers a new personal to-do to be inserted when changes are persisted.
    /// </summary>
    /// <param name="todo">The to-do to add.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task AddAsync(
        PersonalTodo todo,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the personal to-do with the given identifier.
    /// </summary>
    /// <param name="todoId">The identifier of the to-do to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching to-do, or null if no to-do with that identifier exists.</returns>
    Task<PersonalTodo?> GetByIdAsync(
        Guid todoId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Searches for personal to-dos matching the given filter and paging criteria.
    /// </summary>
    /// <param name="criteria">The search, filter, and paging criteria to apply.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The matching page of to-dos along with the total matching count.</returns>
    Task<PersonalTodoSearchResult> SearchAsync(
        PersonalTodoSearchCriteria criteria,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the incomplete to-dos for a given user due strictly before the given date.
    /// </summary>
    /// <param name="userId">The identifier of the user whose to-dos are being retrieved.</param>
    /// <param name="targetDate">The exclusive upper-bound date to check to-dos against.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The user's incomplete to-dos due before the given date.</returns>
    Task<IReadOnlyList<PersonalTodo>> ListIncompleteBeforeAsync(
        Guid userId,
        DateOnly targetDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the incomplete to-dos across all users due strictly before the given date.
    /// </summary>
    /// <param name="targetDate">The exclusive upper-bound date to check to-dos against.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The incomplete to-dos due before the given date, across all users.</returns>
    Task<IReadOnlyList<PersonalTodo>> ListIncompleteBeforeAsync(
        DateOnly targetDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves owner display information for the given set of user identifiers.
    /// </summary>
    /// <param name="userIds">The identifiers of the users to retrieve owner information for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The owner records matching the given user identifiers.</returns>
    Task<IReadOnlyList<PersonalTodoOwner>> ListOwnersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a user's to-dos falling within the given inclusive date range.
    /// </summary>
    /// <param name="userId">The identifier of the user whose to-dos are being retrieved.</param>
    /// <param name="from">The inclusive start date of the range.</param>
    /// <param name="to">The inclusive end date of the range.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The user's to-dos within the given date range.</returns>
    Task<IReadOnlyList<PersonalTodo>> ListForUserBetweenAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    /// <summary>
    /// Registers an existing personal to-do to be removed when changes are persisted.
    /// </summary>
    /// <param name="todo">The to-do to remove.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task RemoveAsync(
        PersonalTodo todo,
        CancellationToken cancellationToken);
}
