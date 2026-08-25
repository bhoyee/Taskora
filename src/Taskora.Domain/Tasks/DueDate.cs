using TodoApp.Domain.Common;

namespace TodoApp.Domain.Tasks;

/// <summary>
/// Value object wrapping a task's due date, guaranteeing it is always a real,
/// explicitly-set date rather than the default/uninitialized value.
/// </summary>
public sealed record DueDate
{
    private DueDate(DateOnly value)
    {
        Value = value;
    }

    public DateOnly Value { get; }

    /// <summary>
    /// Creates a <see cref="DueDate"/>, rejecting <c>default(DateOnly)</c> since that
    /// would indicate a due date was never actually supplied.
    /// </summary>
    public static DueDate Create(DateOnly value)
    {
        if (value == default)
        {
            throw new DomainValidationException("Due date is required.");
        }

        return new DueDate(value);
    }

    /// <summary>
    /// A task is overdue only if it is not yet completed and its due date has passed;
    /// completed tasks are never considered overdue regardless of date.
    /// </summary>
    public bool IsOverdue(DateOnly today, TaskItemStatus status) =>
        status != TaskItemStatus.Completed && Value < today;

    /// <summary>Number of days between today and the due date (negative if already past).</summary>
    public int DaysUntil(DateOnly today) => Value.DayNumber - today.DayNumber;
}
