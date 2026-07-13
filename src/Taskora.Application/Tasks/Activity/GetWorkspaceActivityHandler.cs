using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;

namespace TodoApp.Application.Tasks.Activity;

public sealed record GetWorkspaceActivityQuery(
    Guid WorkspaceId,
    string? Type = null,
    int PageNumber = 1,
    int PageSize = 20);

public sealed class GetWorkspaceActivityHandler(
    ITaskActivityReadRepository activity)
{
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

    private static Result<PagedResult<WorkspaceActivityRecord>> ValidationFailure(
        string description) =>
        Result<PagedResult<WorkspaceActivityRecord>>.Failure(
            new ApplicationError(
                "workspace.activity_validation",
                description,
                ErrorType.Validation));
}
