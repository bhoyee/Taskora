using TodoApp.Domain.Todos;

namespace TodoApp.Api.Contracts;

public sealed record CreatePersonalTodoRequest(
    string Title,
    DateOnly? TodoDate,
    string? Notes,
    TodoPriority? Priority);

public sealed record UpdatePersonalTodoRequest(
    string Title,
    DateOnly? TodoDate,
    string? Notes,
    TodoPriority? Priority);

public sealed record CreateDailyRoutineRequest(
    string Title,
    string? Notes,
    TodoPriority? Priority,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record UpdateDailyRoutineRequest(
    string Title,
    string? Notes,
    TodoPriority? Priority,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsActive);
