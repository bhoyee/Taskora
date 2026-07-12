using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

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
    int PageSize = 20);
