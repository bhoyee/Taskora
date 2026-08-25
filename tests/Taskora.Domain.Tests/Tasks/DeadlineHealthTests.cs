using TodoApp.Domain.Tasks;
using Xunit;

namespace TodoApp.Domain.Tests.Tasks;

// Tests for TaskItem.GetDeadlineHealth, covering the Healthy/AtRisk/Overdue/Completed rules driven by due date and status.
public sealed class DeadlineHealthTests
{
    private static readonly DateOnly Today = new(2026, 7, 4);

    [Fact]
    public void Unscheduled_task_is_healthy() =>
        Assert.Equal(
            DeadlineHealth.Healthy,
            CreateTask().GetDeadlineHealth(Today));

    // Verifies the exact boundary: negative days remaining is Overdue, 0-3 days remaining (inclusive) is AtRisk, 4+ days is Healthy.
    [Theory]
    [InlineData(-1, DeadlineHealth.Overdue)]
    [InlineData(0, DeadlineHealth.AtRisk)]
    [InlineData(3, DeadlineHealth.AtRisk)]
    [InlineData(4, DeadlineHealth.Healthy)]
    public void Active_task_health_uses_due_date_boundary(
        int daysFromToday,
        DeadlineHealth expected)
    {
        var task = CreateTask();
        task.Schedule(DueDate.Create(Today.AddDays(daysFromToday)));

        Assert.Equal(expected, task.GetDeadlineHealth(Today));
    }

    // Completion status takes precedence over the due-date check, even for a date that would otherwise be Overdue.
    [Fact]
    public void Completed_task_is_completed_even_when_due_date_has_passed()
    {
        var task = CreateTask();
        task.Schedule(DueDate.Create(Today.AddDays(-1)));
        task.MoveToReady();
        task.Start();
        task.Complete(new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(
            DeadlineHealth.Completed,
            task.GetDeadlineHealth(Today));
    }

    private static TaskItem CreateTask() =>
        TaskItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Deadline task");
}
