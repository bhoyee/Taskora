using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

/// <summary>
/// DTO representing a single task row returned by search/list endpoints, with just
/// enough data to render a task in a list or board view.
/// </summary>
public sealed record TaskListItemDto(
    Guid Id,
    Guid ProjectId,
    Guid? CreatedByUserId,
    Guid? AssignedUserId,
    Guid? SprintId,
    DateTimeOffset CreatedAt,
    string Title,
    Guid? CategoryId,
    IReadOnlyCollection<string> Tags,
    TaskItemStatus Status,
    bool IsBlocked,
    DateOnly? DueDate,
    decimal? PriorityScore,
    PriorityBand? PriorityBand,
    PriorityExplanationDto? PriorityExplanation,
    DeadlineHealth DeadlineHealth);
