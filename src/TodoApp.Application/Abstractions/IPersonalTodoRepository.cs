using TodoApp.Domain.Todos;

namespace TodoApp.Application.Abstractions;

public interface IPersonalTodoRepository
{
    Task AddAsync(
        PersonalTodo todo,
        CancellationToken cancellationToken);

    Task<PersonalTodo?> GetByIdAsync(
        Guid todoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PersonalTodo>> ListForUserAsync(
        Guid userId,
        DateOnly? date,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        PersonalTodo todo,
        CancellationToken cancellationToken);
}
