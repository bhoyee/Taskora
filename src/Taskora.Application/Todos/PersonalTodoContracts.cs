using TodoApp.Domain.Todos;

namespace TodoApp.Application.Todos;

public sealed record PersonalTodoDto(
    Guid Id,
    string Title,
    DateOnly TodoDate,
    DateOnly OriginalTodoDate,
    DateOnly? CarriedOverFromDate,
    string? Notes,
    TodoPriority Priority,
    Guid? DailyRoutineId,
    bool IsGeneratedFromDailyRoutine,
    bool IsCompleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record DailyRoutineDto(
    Guid Id,
    string Title,
    string? Notes,
    TodoPriority Priority,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    DateOnly? LastGeneratedDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ListPersonalTodosQuery(
    DateOnly? Date,
    string? Search,
    int PageNumber,
    int PageSize);

public sealed record CreatePersonalTodoCommand(
    string Title,
    DateOnly TodoDate,
    string? Notes,
    TodoPriority Priority);

public sealed record UpdatePersonalTodoCommand(
    Guid TodoId,
    string Title,
    DateOnly TodoDate,
    string? Notes,
    TodoPriority Priority);

public sealed record CompletePersonalTodoCommand(Guid TodoId);

public sealed record ReopenPersonalTodoCommand(Guid TodoId);

public sealed record DeletePersonalTodoCommand(Guid TodoId);

public sealed record ListDailyRoutinesQuery(
    int PageNumber,
    int PageSize);

public sealed record CreateDailyRoutineCommand(
    string Title,
    string? Notes,
    TodoPriority Priority,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record UpdateDailyRoutineCommand(
    Guid RoutineId,
    string Title,
    string? Notes,
    TodoPriority Priority,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive);

public sealed record DeleteDailyRoutineCommand(Guid RoutineId);

public sealed record GenerateDailyRoutineTodosCommand(DateOnly? BusinessDate);

public sealed record GenerateDailyRoutineTodosResult(
    int GeneratedCount,
    int SkippedCount,
    DateOnly BusinessDate);
