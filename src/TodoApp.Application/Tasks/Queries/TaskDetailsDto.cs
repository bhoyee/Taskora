using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

public sealed record TaskDetailsDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    TaskItemStatus Status,
    bool IsBlocked,
    string? BlockedReason,
    DateOnly? DueDate,
    int? Effort,
    decimal? PriorityScore,
    IReadOnlyCollection<Guid> DependencyIds,
    DateTimeOffset? CompletedAt);
