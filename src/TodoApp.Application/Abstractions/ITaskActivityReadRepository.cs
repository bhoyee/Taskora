namespace TodoApp.Application.Abstractions;

public interface ITaskActivityReadRepository
{
    Task<IReadOnlyList<TaskActivityRecord>> GetForTaskAsync(
        Guid taskId,
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
