using TodoApp.Application.Common;

namespace TodoApp.Application.Abstractions;

public interface ITaskActivityReadRepository
{
    Task<IReadOnlyList<TaskActivityRecord>> GetForTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken);

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
