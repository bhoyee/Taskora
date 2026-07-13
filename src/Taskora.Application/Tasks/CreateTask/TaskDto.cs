using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.CreateTask;

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
