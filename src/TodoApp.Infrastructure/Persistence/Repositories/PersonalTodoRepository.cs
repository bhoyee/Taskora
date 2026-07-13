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

    public async Task<IReadOnlyList<PersonalTodo>> ListForUserAsync(
        Guid userId,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var query = context.PersonalTodos
            .AsNoTracking()
            .Where(todo => todo.UserId == userId);

        if (date.HasValue)
        {
            query = query.Where(todo => todo.TodoDate == date.Value);
        }

        return await query
            .OrderBy(todo => todo.IsCompleted)
            .ThenBy(todo => todo.TodoDate)
            .ThenByDescending(todo => todo.CreatedAt)
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
