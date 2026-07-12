using TodoApp.Application.Tasks.Metadata;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

public sealed record TaskDetailsDto(
    Guid Id,
    Guid ProjectId,
    Guid? CreatedByUserId,
    Guid? AssignedUserId,
    DateTimeOffset CreatedAt,
    string Title,
    Guid? CategoryId,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<TaskNoteDto> Notes,
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
