using TodoApp.Domain.Common;

namespace TodoApp.Domain.Tasks;

public sealed record DueDate
{
    private DueDate(DateOnly value)
    {
        Value = value;
    }

    public DateOnly Value { get; }

    public static DueDate Create(DateOnly value)
    {
        if (value == default)
        {
            throw new DomainValidationException("Due date is required.");
        }

        return new DueDate(value);
    }

    public bool IsOverdue(DateOnly today, TaskItemStatus status) =>
        status != TaskItemStatus.Completed && Value < today;

    public int DaysUntil(DateOnly today) => Value.DayNumber - today.DayNumber;
}
