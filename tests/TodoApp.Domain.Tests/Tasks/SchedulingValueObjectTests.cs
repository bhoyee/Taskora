using TodoApp.Domain.Common;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Domain.Tests.Tasks;

public sealed class SchedulingValueObjectTests
{
    [Fact]
    public void DueDate_Create_WithValidDate_PreservesDate()
    {
        var date = new DateOnly(2026, 7, 15);

        var dueDate = DueDate.Create(date);

        Assert.Equal(date, dueDate.Value);
    }

    [Fact]
    public void DueDate_Create_WithDefaultDate_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => DueDate.Create(default));

        Assert.Equal("Due date is required.", exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public void EffortEstimate_Create_WithSupportedValue_PreservesValue(int value)
    {
        var estimate = EffortEstimate.Create(value);

        Assert.Equal(value, estimate.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(9)]
    public void EffortEstimate_Create_WithUnsupportedValue_ThrowsDomainValidationException(
        int value)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => EffortEstimate.Create(value));

        Assert.Equal(
            "Effort must be one of 1, 2, 3, 5, or 8.",
            exception.Message);
    }

    [Fact]
    public void IsOverdue_WhenDueDateHasPassedAndTaskIsOpen_ReturnsTrue()
    {
        var dueDate = DueDate.Create(new DateOnly(2026, 7, 1));

        var isOverdue = dueDate.IsOverdue(
            new DateOnly(2026, 7, 2),
            TaskItemStatus.InProgress);

        Assert.True(isOverdue);
    }

    [Fact]
    public void IsOverdue_WhenTaskIsCompleted_ReturnsFalse()
    {
        var dueDate = DueDate.Create(new DateOnly(2026, 7, 1));

        var isOverdue = dueDate.IsOverdue(
            new DateOnly(2026, 7, 2),
            TaskItemStatus.Completed);

        Assert.False(isOverdue);
    }

    [Fact]
    public void Schedule_WhenDueDateIsProvided_UpdatesTaskDueDate()
    {
        var task = TaskItem.Create(Guid.NewGuid(), "Publish portfolio");
        var dueDate = DueDate.Create(new DateOnly(2026, 7, 15));

        task.Schedule(dueDate);

        Assert.Same(dueDate, task.DueDate);
    }

    [Fact]
    public void Estimate_WhenEffortIsProvided_UpdatesTaskEstimate()
    {
        var task = TaskItem.Create(Guid.NewGuid(), "Publish portfolio");
        var effort = EffortEstimate.Create(5);

        task.Estimate(effort);

        Assert.Same(effort, task.EffortEstimate);
    }

    [Fact]
    public void SetTargetDate_WhenProjectIsActive_UpdatesProjectTargetDate()
    {
        var project = Project.Create(Guid.NewGuid(), "Portfolio launch");
        var targetDate = DueDate.Create(new DateOnly(2026, 7, 31));

        project.SetTargetDate(targetDate);

        Assert.Same(targetDate, project.TargetDate);
    }
}
