using TodoApp.Domain.Todos;

namespace TodoApp.Application.Todos;

/// <summary>Represents a personal ("My Day") todo item, including its carry-over history and comments.</summary>
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
    DateTimeOffset? CompletedAt,
    IReadOnlyList<PersonalTodoCommentDto> Comments);

/// <summary>Represents a comment left on a personal todo.</summary>
public sealed record PersonalTodoCommentDto(
    Guid Id,
    Guid TodoId,
    string Body,
    DateTimeOffset CreatedAt);

/// <summary>Represents a recurring daily routine used to auto-generate personal todos.</summary>
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

/// <summary>Query to list the current user's personal todos for a date (or today), with optional search and paging.</summary>
public sealed record ListPersonalTodosQuery(
    DateOnly? Date,
    string? Search,
    int PageNumber,
    int PageSize);

/// <summary>Query to list the current user's personal todos across an inclusive date range.</summary>
public sealed record ListPersonalTodosForRangeQuery(
    DateOnly From,
    DateOnly To);

/// <summary>Command to create a new personal todo for the current user.</summary>
public sealed record CreatePersonalTodoCommand(
    string Title,
    DateOnly TodoDate,
    string? Notes,
    TodoPriority Priority);

/// <summary>Command to update an existing personal todo owned by the current user.</summary>
public sealed record UpdatePersonalTodoCommand(
    Guid TodoId,
    string Title,
    DateOnly TodoDate,
    string? Notes,
    TodoPriority Priority);

/// <summary>Command to mark a personal todo as completed.</summary>
public sealed record CompletePersonalTodoCommand(Guid TodoId);

/// <summary>Command to reopen a previously completed personal todo.</summary>
public sealed record ReopenPersonalTodoCommand(Guid TodoId);

/// <summary>Command to delete a personal todo owned by the current user.</summary>
public sealed record DeletePersonalTodoCommand(Guid TodoId);

/// <summary>Command to add a comment to a personal todo.</summary>
public sealed record AddPersonalTodoCommentCommand(Guid TodoId, string Body);

/// <summary>Query to list the current user's daily routines, with paging.</summary>
public sealed record ListDailyRoutinesQuery(
    int PageNumber,
    int PageSize);

/// <summary>Command to create a new daily routine for the current user.</summary>
public sealed record CreateDailyRoutineCommand(
    string Title,
    string? Notes,
    TodoPriority Priority,
    DateOnly StartDate,
    DateOnly? EndDate);

/// <summary>Command to update an existing daily routine owned by the current user.</summary>
public sealed record UpdateDailyRoutineCommand(
    Guid RoutineId,
    string Title,
    string? Notes,
    TodoPriority Priority,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive);

/// <summary>Command to delete a daily routine owned by the current user.</summary>
public sealed record DeleteDailyRoutineCommand(Guid RoutineId);

/// <summary>Command to generate personal todos from due daily routines for a business date (or today).</summary>
public sealed record GenerateDailyRoutineTodosCommand(DateOnly? BusinessDate);

/// <summary>Summarizes the outcome of a daily-routine todo generation run.</summary>
public sealed record GenerateDailyRoutineTodosResult(
    int GeneratedCount,
    int SkippedCount,
    DateOnly BusinessDate);
