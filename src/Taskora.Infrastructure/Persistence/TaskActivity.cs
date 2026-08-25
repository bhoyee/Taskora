namespace TodoApp.Infrastructure.Persistence;

/// <summary>
/// Append-only audit log entry recording a single change made to a
/// <c>TaskItem</c> (status transition, field edit, tag/note addition, etc.).
/// Rows are created automatically by <see cref="TodoAppDbContext"/> when it
/// detects tracked changes, so this type has no public mutators — it is
/// built exclusively through the factory methods below.
/// </summary>
public sealed class TaskActivity
{
    // Reserved for EF Core materialization; entities are otherwise only
    // constructed through the factory methods below.
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

    /// <summary>Database-generated ordering key used to replay activity in insertion order.</summary>
    public long Sequence { get; private set; }

    /// <summary>Identifier of the task this activity entry belongs to.</summary>
    public Guid TaskId { get; private set; }

    /// <summary>User id (or "system") responsible for the change.</summary>
    public string Actor { get; private set; } = string.Empty;

    /// <summary>Short code identifying the kind of change, e.g. "StatusChanged", "TaskRenamed".</summary>
    public string ActivityType { get; private set; } = string.Empty;

    /// <summary>Field value before the change, formatted for display; empty when not applicable.</summary>
    public string PreviousValue { get; private set; } = string.Empty;

    /// <summary>Field value after the change, formatted for display.</summary>
    public string CurrentValue { get; private set; } = string.Empty;

    /// <summary>Timestamp (UTC) at which the change was persisted.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Creates an activity entry specifically for a task status transition.</summary>
    public static TaskActivity StatusChanged(
        Guid taskId,
        string previousValue,
        string currentValue,
        DateTimeOffset occurredAt,
        string actor = "system") =>
        Record(
            taskId,
            "StatusChanged",
            previousValue,
            currentValue,
            occurredAt,
            actor);

    /// <summary>Creates a general-purpose activity entry for any tracked change type.</summary>
    public static TaskActivity Record(
        Guid taskId,
        string activityType,
        string previousValue,
        string currentValue,
        DateTimeOffset occurredAt,
        string actor = "system") =>
        new(
            taskId,
            actor,
            activityType,
            previousValue,
            currentValue,
            occurredAt);
}
