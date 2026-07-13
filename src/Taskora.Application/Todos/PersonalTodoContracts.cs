namespace TodoApp.Application.Todos;

public sealed record PersonalTodoDto(
    Guid Id,
    string Title,
    DateOnly TodoDate,
    DateOnly OriginalTodoDate,
    DateOnly? CarriedOverFromDate,
    string? Notes,
    bool IsCompleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record ListPersonalTodosQuery(
    DateOnly? Date,
    string? Search,
    int PageNumber,
    int PageSize);

public sealed record CreatePersonalTodoCommand(
    string Title,
    DateOnly TodoDate,
    string? Notes);

public sealed record UpdatePersonalTodoCommand(
    Guid TodoId,
    string Title,
    DateOnly TodoDate,
    string? Notes);

public sealed record CompletePersonalTodoCommand(Guid TodoId);

public sealed record ReopenPersonalTodoCommand(Guid TodoId);

public sealed record DeletePersonalTodoCommand(Guid TodoId);
