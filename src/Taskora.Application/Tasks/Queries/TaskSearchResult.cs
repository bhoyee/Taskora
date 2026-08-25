using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

/// <summary>
/// Raw result of a task search against the repository: the page of matching domain
/// tasks along with the total count across all pages (used for pagination metadata).
/// </summary>
public sealed record TaskSearchResult(
    IReadOnlyList<TaskItem> Items,
    int TotalCount);
