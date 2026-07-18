using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class PersonalTodoRepository(TodoAppDbContext context)
    : IPersonalTodoRepository
{
    public async Task AddAsync(
        PersonalTodo todo,
        CancellationToken cancellationToken)
    {
        await context.PersonalTodos.AddAsync(todo, cancellationToken);
    }

    public Task<PersonalTodo?> GetByIdAsync(
        Guid todoId,
        CancellationToken cancellationToken) =>
        context.PersonalTodos
            .FirstOrDefaultAsync(todo => todo.Id == todoId, cancellationToken);

    public async Task<PersonalTodoSearchResult> SearchAsync(
        PersonalTodoSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var query = context.PersonalTodos
            .AsNoTracking()
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

    public Task RemoveAsync(
        PersonalTodo todo,
        CancellationToken cancellationToken)
    {
        context.PersonalTodos.Remove(todo);
        return Task.CompletedTask;
    }
}
