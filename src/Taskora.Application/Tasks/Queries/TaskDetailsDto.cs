using TodoApp.Application.Tasks.Metadata;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

/// <summary>
/// DTO representing the full detail view of a single task, including its notes,
/// tags, dependencies, and computed priority/deadline information.
/// </summary>
public sealed record TaskDetailsDto(
    Guid Id,
    Guid ProjectId,
    Guid? CreatedByUserId,
    Guid? AssignedUserId,
    Guid? SprintId,
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
