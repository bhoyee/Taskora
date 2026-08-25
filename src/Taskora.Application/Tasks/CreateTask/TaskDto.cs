using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.CreateTask;

/// <summary>
/// Read-only projection of a task's key attributes, returned from task creation and other
/// application-layer operations.
/// </summary>
public sealed record TaskDto(
    Guid Id,
    Guid ProjectId,
    Guid? CreatedByUserId,
    Guid? SprintId,
    DateTimeOffset CreatedAt,
    string Title,
    TaskItemStatus Status,
    DateOnly? DueDate,
    int? Effort);
