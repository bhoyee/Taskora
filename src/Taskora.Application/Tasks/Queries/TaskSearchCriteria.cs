using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

/// <summary>
/// Normalized filter/sort/paging criteria passed to <c>ITaskReadRepository.SearchAsync</c>,
/// built from a <see cref="SearchTasksQuery"/> after input trimming/normalization.
/// </summary>
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
