using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Projects.Board;

public sealed record GetProjectBoardQuery(Guid ProjectId);

public sealed record ProjectBoardSnapshot(
    int BacklogCount,
    int ReadyCount,
    int InProgressCount,
    int BlockedCount,
    int CompletedCount,
    int OverdueCount,
    IReadOnlyList<TaskItem> HighPriorityBlockedTasks);

public sealed record HighPriorityBlockedTaskDto(
    Guid Id,
    string Title,
    decimal PriorityScore,
    IReadOnlyCollection<Guid> DependencyIds);

public sealed record ProjectBoardDto(
    Guid ProjectId,
    string ProjectName,
    int BacklogCount,
    int ReadyCount,
    int InProgressCount,
    int BlockedCount,
    int CompletedCount,
    int OverdueCount,
    IReadOnlyList<HighPriorityBlockedTaskDto> HighPriorityBlockedTasks)
{
    public int TotalTasks =>
        BacklogCount +
        ReadyCount +
        InProgressCount +
        BlockedCount +
        CompletedCount;
}
