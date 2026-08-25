using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for the <see cref="PersonalTodo"/> aggregate ("My Day" todos),
/// covering creation, tracked lookup, filtered/paged search, carry-over
/// listings, and owner lookups.
/// </summary>
public sealed class PersonalTodoRepository(TodoAppDbContext context)
    : IPersonalTodoRepository
{
    /// <summary>Stages a new todo for insertion; persistence happens on unit-of-work save.</summary>
    public async Task AddAsync(
        PersonalTodo todo,
        CancellationToken cancellationToken)
    {
        await context.PersonalTodos.AddAsync(todo, cancellationToken);
    }

    /// <summary>
    /// Loads a tracked todo by id for mutation. Explicitly includes the
    /// <c>_comments</c> backing collection by shadow name since it is not an
    /// owned navigation that loads automatically.
    /// </summary>
    public Task<PersonalTodo?> GetByIdAsync(
        Guid todoId,
        CancellationToken cancellationToken) =>
        context.PersonalTodos
            .Include("_comments")
            .FirstOrDefaultAsync(todo => todo.Id == todoId, cancellationToken);

    /// <summary>
    /// Returns a paged, optionally date- and text-filtered list of a user's
    /// todos (incomplete first, then by date, then newest-created), along
    /// with the total match count. Uses <c>AsNoTracking()</c> for the
    /// read-only listing but still includes the shadow <c>_comments</c>
    /// collection so callers can display comment info without extra queries.
    /// The title/notes search uses <c>string.Contains</c> rather than
    /// <c>EF.Functions.Like</c>, which both providers translate to a
    /// case-sensitivity behavior governed by the underlying column
    /// collation.
    /// </summary>
    public async Task<PersonalTodoSearchResult> SearchAsync(
        PersonalTodoSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var query = context.PersonalTodos
            .AsNoTracking()
            .Include("_comments")
            .Where(todo => todo.UserId == criteria.UserId);

        if (criteria.Date.HasValue)
        {
            query = query.Where(todo => todo.TodoDate == criteria.Date.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var search = criteria.Search.Trim();
            query = query.Where(todo =>
                todo.Title.Contains(search) ||
                (todo.Notes != null && todo.Notes.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(todo => todo.IsCompleted)
            .ThenBy(todo => todo.TodoDate)
            .ThenByDescending(todo => todo.CreatedAt)
            .Skip((criteria.PageNumber - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PersonalTodoSearchResult(items, totalCount);
    }

    /// <summary>
    /// Lists a single user's incomplete todos dated before
    /// <paramref name="targetDate"/>, tracked (no <c>AsNoTracking()</c>) since
    /// this is used by the carry-over job to update <c>TodoDate</c>/
    /// <c>CarriedOverFromDate</c> on the returned entities.
    /// </summary>
    public async Task<IReadOnlyList<PersonalTodo>> ListIncompleteBeforeAsync(
        Guid userId,
        DateOnly targetDate,
        CancellationToken cancellationToken)
    {
        return await context.PersonalTodos
            .Where(todo =>
                todo.UserId == userId &&
                !todo.IsCompleted &&
                todo.TodoDate < targetDate)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Lists incomplete todos across all users dated before
    /// <paramref name="targetDate"/>, ordered by user then date then
    /// newest-created; used by the platform-wide carry-over background job.
    /// Also tracked rather than <c>AsNoTracking()</c> since callers mutate
    /// the returned entities to carry them into the new business date.
    /// </summary>
    public async Task<IReadOnlyList<PersonalTodo>> ListIncompleteBeforeAsync(
        DateOnly targetDate,
        CancellationToken cancellationToken)
    {
        return await context.PersonalTodos
            .Where(todo =>
                !todo.IsCompleted &&
                todo.TodoDate < targetDate)
            .OrderBy(todo => todo.UserId)
            .ThenBy(todo => todo.TodoDate)
            .ThenByDescending(todo => todo.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Resolves display info (id, name, email) for a set of todo owner ids as
    /// a read-only projection; returns an empty list without querying if no
    /// ids are supplied.
    /// </summary>
    public async Task<IReadOnlyList<PersonalTodoOwner>> ListOwnersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await context.UserProfiles
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new PersonalTodoOwner(
                user.Id,
                user.DisplayName,
                user.Email))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Lists a user's todos with a date in the inclusive [<paramref name="from"/>,
    /// <paramref name="to"/>] range, ordered by date, as a read-only
    /// (<c>AsNoTracking()</c>) projection.
    /// </summary>
    public async Task<IReadOnlyList<PersonalTodo>> ListForUserBetweenAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        return await context.PersonalTodos
            .AsNoTracking()
            .Where(todo =>
                todo.UserId == userId &&
                todo.TodoDate >= from &&
                todo.TodoDate <= to)
            .OrderBy(todo => todo.TodoDate)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>Stages a todo for deletion; persistence happens on unit-of-work save.</summary>
    public Task RemoveAsync(
        PersonalTodo todo,
        CancellationToken cancellationToken)
    {
        context.PersonalTodos.Remove(todo);
        return Task.CompletedTask;
    }
}
