namespace TodoApp.Domain.Tasks;

/// <summary>
/// Derived indicator of how a task's due date is tracking, used to surface
/// deadline risk without requiring the caller to recompute date math.
/// </summary>
public enum DeadlineHealth
{
    /// <summary>Due date is comfortably in the future.</summary>
    Healthy = 0,

    /// <summary>Due date is approaching soon enough to warrant attention.</summary>
    AtRisk = 1,

    /// <summary>Due date has passed and the task is not complete.</summary>
    Overdue = 2,

    /// <summary>Task is finished, so deadline health is no longer a concern.</summary>
    Completed = 3
}
