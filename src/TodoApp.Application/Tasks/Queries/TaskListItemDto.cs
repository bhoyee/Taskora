using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

public sealed record TaskListItemDto(
    Guid Id,
    Guid ProjectId,
    Guid? AssignedUserId,
    string Title,
    TaskItemStatus Status,
    bool IsBlocked,
    DateOnly? DueDate,
    decimal? PriorityScore,
    PriorityBand? PriorityBand,
    PriorityExplanationDto? PriorityExplanation,
    DeadlineHealth DeadlineHealth);
