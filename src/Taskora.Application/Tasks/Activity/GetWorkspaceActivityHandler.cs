using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;

namespace TodoApp.Application.Tasks.Activity;

/// <summary>
/// Query for a paged activity feed scoped to a workspace, optionally filtered by
/// activity <paramref name="Type"/>.
/// </summary>
public sealed record GetWorkspaceActivityQuery(
    Guid WorkspaceId,
    string? Type = null,
    int PageNumber = 1,
    int PageSize = 20);

/// <summary>
/// Handles fetching a paged activity feed for a workspace.
/// </summary>
public sealed class GetWorkspaceActivityHandler(
    ITaskActivityReadRepository activity)
{
    /// <summary>
    /// Validates the workspace identifier and paging parameters, then returns the
    /// activity records for that workspace (optionally filtered by type) as a paged
    /// result. Scoping to <see cref="GetWorkspaceActivityQuery.WorkspaceId"/> ensures
    /// only activity belonging to that workspace is returned.
    /// </summary>
    public async Task<Result<PagedResult<WorkspaceActivityRecord>>> HandleAsync(
        GetWorkspaceActivityQuery query,
        CancellationToken cancellationToken)
    {
        if (query.WorkspaceId == Guid.Empty)
        {
            return ValidationFailure("Workspace identifier is required.");
        }

        if (query.PageNumber < 1)
        {
            return ValidationFailure("Page number must be at least 1.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            return ValidationFailure("Page size must be between 1 and 100.");
        }

        return Result<PagedResult<WorkspaceActivityRecord>>.Success(
            await activity.GetForWorkspaceAsync(
                query.WorkspaceId,
                query.Type,
                query.PageNumber,
                query.PageSize,
                cancellationToken));
    }

    // Builds a validation-error Result for bad workspace id / paging input.
    private static Result<PagedResult<WorkspaceActivityRecord>> ValidationFailure(
        string description) =>
        Result<PagedResult<WorkspaceActivityRecord>>.Failure(
            new ApplicationError(
                "workspace.activity_validation",
                description,
                ErrorType.Validation));
}
