using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

public sealed record TaskSearchCriteria(
    Guid? ProjectId,
    Guid? WorkspaceId,
    TaskItemStatus? Status,
    bool? IsBlocked,
    Guid? CategoryId,
    string? Tag,
    string? Search,
    TaskSortBy SortBy,
    int PageNumber,
    int PageSize,
    Guid? SprintId = null);
