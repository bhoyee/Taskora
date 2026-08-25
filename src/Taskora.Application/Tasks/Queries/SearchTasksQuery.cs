using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

/// <summary>
/// Query for a filtered, sorted, paged list of tasks. All filter parameters are
/// optional; omitting them broadens the result set within whatever project/workspace
/// scope is supplied.
/// </summary>
public sealed record SearchTasksQuery(
    Guid? ProjectId = null,
    Guid? WorkspaceId = null,
    TaskItemStatus? Status = null,
    bool? IsBlocked = null,
    Guid? CategoryId = null,
    string? Tag = null,
    string? Search = null,
    TaskSortBy SortBy = TaskSortBy.CreatedDescending,
    int PageNumber = 1,
    int PageSize = 20,
    Guid? SprintId = null);
