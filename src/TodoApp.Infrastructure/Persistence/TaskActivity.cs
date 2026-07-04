namespace TodoApp.Infrastructure.Persistence;

public sealed class TaskActivity
{
    private TaskActivity()
    {
    }

    private TaskActivity(
        Guid taskId,
        string actor,
        string activityType,
        string previousValue,
        string currentValue,
        DateTimeOffset occurredAt)
    {
        TaskId = taskId;
        Actor = actor;
        ActivityType = activityType;
        PreviousValue = previousValue;
        CurrentValue = currentValue;
        OccurredAt = occurredAt;
    }

    public long Sequence { get; private set; }

    public Guid TaskId { get; private set; }

    public string Actor { get; private set; } = string.Empty;

    public string ActivityType { get; private set; } = string.Empty;

    public string PreviousValue { get; private set; } = string.Empty;

    public string CurrentValue { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public static TaskActivity StatusChanged(
        Guid taskId,
        string previousValue,
        string currentValue,
        DateTimeOffset occurredAt) =>
        new(
            taskId,
            "system",
            "StatusChanged",
            previousValue,
            currentValue,
            occurredAt);
}
