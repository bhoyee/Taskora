using TodoApp.Domain.Todos;

namespace TodoApp.Api.Contracts;

/// <summary>Request body for creating a personal (non-project) to-do item.</summary>
public sealed record CreatePersonalTodoRequest(
    string Title,
    DateOnly? TodoDate,
    string? Notes,
    TodoPriority? Priority);

/// <summary>Request body for updating a personal to-do item.</summary>
public sealed record UpdatePersonalTodoRequest(
    string Title,
    DateOnly? TodoDate,
    string? Notes,
    TodoPriority? Priority);

/// <summary>Request body for adding a comment to a personal to-do item.</summary>
public sealed record AddPersonalTodoCommentRequest(string Body);

/// <summary>Request body for creating a recurring daily routine.</summary>
public sealed record CreateDailyRoutineRequest(
    string Title,
    string? Notes,
    TodoPriority? Priority,
    DateOnly? StartDate,
    DateOnly? EndDate);

/// <summary>Request body for updating a recurring daily routine, including whether it is active.</summary>
public sealed record UpdateDailyRoutineRequest(
    string Title,
    string? Notes,
    TodoPriority? Priority,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsActive);
