using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

public sealed record TaskSearchCriteria(
    Guid? ProjectId,
    TaskItemStatus? Status,
    bool? IsBlocked,
    string? Search,
    TaskSortBy SortBy,
    int PageNumber,
    int PageSize);
