using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Projects.Board;

/// <summary>Query for the board snapshot of a single project.</summary>
public sealed record GetProjectBoardQuery(Guid ProjectId);

/// <summary>
/// Raw per-status task counts and high-priority blocked tasks for a project's
/// board, as produced by the board read repository.
/// </summary>
public sealed record ProjectBoardSnapshot(
    int BacklogCount,
    int ReadyCount,
    int InProgressCount,
    int BlockedCount,
    int CompletedCount,
    int OverdueCount,
    int AtRiskCount,
    int CriticalCount,
    IReadOnlyList<TaskItem> HighPriorityBlockedTasks);

/// <summary>Represents a high-priority task that is currently blocked by incomplete dependencies.</summary>
public sealed record HighPriorityBlockedTaskDto(
    Guid Id,
    string Title,
    decimal PriorityScore,
    IReadOnlyCollection<Guid> IncompleteDependencyChainIds);

/// <summary>Represents the board view of a project: per-status task counts plus blocked high-priority tasks.</summary>
public sealed record ProjectBoardDto(
    Guid ProjectId,
    string ProjectName,
    int BacklogCount,
    int ReadyCount,
    int InProgressCount,
    int BlockedCount,
    int CompletedCount,
    int OverdueCount,
    int AtRiskCount,
    int CriticalCount,
    IReadOnlyList<HighPriorityBlockedTaskDto> HighPriorityBlockedTasks)
{
    /// <summary>The total number of tasks across all status buckets.</summary>
    public int TotalTasks =>
        BacklogCount +
        ReadyCount +
        InProgressCount +
        BlockedCount +
        CompletedCount;
}
