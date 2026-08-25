using TodoApp.Application.Common;

namespace TodoApp.Application.Abstractions;

/// <summary>
/// Read-only repository for querying the audit/activity trail recorded against tasks.
/// </summary>
public interface ITaskActivityReadRepository
{
    /// <summary>
    /// Retrieves the full activity history for a single task, in occurrence order.
    /// </summary>
    /// <param name="taskId">The identifier of the task whose activity is being retrieved.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The activity records for the given task.</returns>
    Task<IReadOnlyList<TaskActivityRecord>> GetForTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a paged, optionally filtered feed of task activity across an entire workspace.
    /// </summary>
    /// <param name="workspaceId">The identifier of the workspace whose activity feed is being retrieved.</param>
    /// <param name="type">An optional activity type/action to filter by, or null to include all types.</param>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The maximum number of records to return per page.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The requested page of workspace activity records.</returns>
    Task<PagedResult<WorkspaceActivityRecord>> GetForWorkspaceAsync(
        Guid workspaceId,
        string? type,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record TaskActivityRecord(
    long Sequence,
    Guid TaskId,
    string Actor,
    string Action,
    string PreviousValue,
    string CurrentValue,
    DateTimeOffset OccurredAt);

public sealed record WorkspaceActivityRecord(
    long Sequence,
    Guid TaskId,
    string TaskTitle,
    Guid ProjectId,
    string ProjectName,
    string Actor,
    string Action,
    string PreviousValue,
    string CurrentValue,
    DateTimeOffset OccurredAt);
