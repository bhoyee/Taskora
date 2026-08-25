using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;

namespace TodoApp.Application.Tasks.Queries;

/// <summary>
/// Handles filtered, sorted, paged searches over tasks.
/// </summary>
public sealed class SearchTasksHandler(
    ITaskReadRepository tasks,
    IClock clock)
{
    /// <summary>
    /// Validates paging parameters, normalizes the tag/search filters (trimming
    /// whitespace, stripping a leading '#' from tags, lower-casing tags for
    /// case-insensitive matching), then runs the search against the repository
    /// using the caller-supplied project/workspace/sprint/status/category scope.
    /// Maps each returned domain task to a <see cref="TaskListItemDto"/> (computing
    /// deadline health against today's date) and returns a paged result along with
    /// the total match count for pagination.
    /// </summary>
    public async Task<Result<PagedResult<TaskListItemDto>>> HandleAsync(
        SearchTasksQuery query,
        CancellationToken cancellationToken)
    {
        if (query.PageNumber < 1)
        {
            return ValidationFailure("Page number must be at least 1.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            return ValidationFailure("Page size must be between 1 and 100.");
        }

        var criteria = new TaskSearchCriteria(
            query.ProjectId,
            query.WorkspaceId,
            query.Status,
            query.IsBlocked,
            query.CategoryId,
            string.IsNullOrWhiteSpace(query.Tag)
                ? null
                : query.Tag.Trim().TrimStart('#').ToLowerInvariant(),
            string.IsNullOrWhiteSpace(query.Search)
                ? null
                : query.Search.Trim(),
            query.SortBy,
            query.PageNumber,
            query.PageSize,
            query.SprintId);
        var searchResult = await tasks.SearchAsync(
            criteria,
            cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var items = searchResult.Items
            .Select(task => TaskDtoMapper.ToListItem(task, today))
            .ToArray();

        return Result<PagedResult<TaskListItemDto>>.Success(
            new PagedResult<TaskListItemDto>(
                items,
                searchResult.TotalCount,
                query.PageNumber,
                query.PageSize));
    }

    // Builds a validation-error Result for bad paging input.
    private static Result<PagedResult<TaskListItemDto>> ValidationFailure(
        string description) =>
        Result<PagedResult<TaskListItemDto>>.Failure(
            new ApplicationError(
                "task.search_validation",
                description,
                ErrorType.Validation));
}
