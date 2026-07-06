using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

public sealed record TaskDetailsDto(
    Guid Id,
    Guid ProjectId,
    Guid? AssignedUserId,
    string Title,
    TaskItemStatus Status,
    bool IsBlocked,
    string? BlockedReason,
    DateOnly? DueDate,
    int? Effort,
    decimal? PriorityScore,
    PriorityBand? PriorityBand,
    PriorityExplanationDto? PriorityExplanation,
    DeadlineHealth DeadlineHealth,
    IReadOnlyCollection<Guid> DependencyIds,
    DateTimeOffset? CompletedAt);
