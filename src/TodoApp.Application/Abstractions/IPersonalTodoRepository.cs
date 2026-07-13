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

public interface IPersonalTodoRepository
{
    Task AddAsync(
        PersonalTodo todo,
        CancellationToken cancellationToken);

    Task<PersonalTodo?> GetByIdAsync(
        Guid todoId,
        CancellationToken cancellationToken);

    Task<PersonalTodoSearchResult> SearchAsync(
        PersonalTodoSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PersonalTodo>> ListIncompleteBeforeAsync(
        Guid userId,
        DateOnly targetDate,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        PersonalTodo todo,
        CancellationToken cancellationToken);
}
