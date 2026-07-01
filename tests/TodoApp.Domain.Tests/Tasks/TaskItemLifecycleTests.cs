using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Domain.Tests.Tasks;

public sealed class TaskItemLifecycleTests
{
    private static readonly Guid TaskId = Guid.Parse("6fc11d29-d884-4dd6-ab06-4f205dcae65d");

    [Fact]
    public void Create_WhenTitleIsValid_CreatesBacklogTask()
    {
        var task = TaskItem.Create(TaskId, "Prepare release notes");

        Assert.Equal(TaskId, task.Id);
        Assert.Equal("Prepare release notes", task.Title);
        Assert.Equal(TaskItemStatus.Backlog, task.Status);
        Assert.Null(task.CompletedAt);
        Assert.Null(task.BlockedReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenTitleIsBlank_ThrowsDomainValidationException(string title)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => TaskItem.Create(TaskId, title));

        Assert.Equal("Task title is required.", exception.Message);
    }

    [Fact]
    public void MoveToReady_WhenTaskIsInBacklog_ChangesStatusToReady()
    {
        var task = CreateTask();

        task.MoveToReady();

        Assert.Equal(TaskItemStatus.Ready, task.Status);
    }

    [Fact]
    public void Start_WhenTaskIsReady_ChangesStatusToInProgress()
    {
        var task = CreateReadyTask();

        task.Start();

        Assert.Equal(TaskItemStatus.InProgress, task.Status);
    }

    [Fact]
    public void Start_WhenTaskIsInBacklog_ThrowsDomainRuleException()
    {
        var task = CreateTask();

        var exception = Assert.Throws<DomainRuleException>(task.Start);

        Assert.Equal("Only a ready task can be started.", exception.Message);
    }

    [Fact]
    public void Block_WhenTaskIsInProgress_RecordsReason()
    {
        var task = CreateInProgressTask();

        task.Block("Waiting for security review");

        Assert.Equal(TaskItemStatus.Blocked, task.Status);
        Assert.Equal("Waiting for security review", task.BlockedReason);
    }

    [Fact]
    public void Unblock_WhenTaskIsBlocked_ReturnsTaskToReady()
    {
        var task = CreateInProgressTask();
        task.Block("Waiting for security review");

        task.Unblock();

        Assert.Equal(TaskItemStatus.Ready, task.Status);
        Assert.Null(task.BlockedReason);
    }

    [Fact]
    public void Complete_WhenTaskIsInProgress_RecordsCompletionTime()
    {
        var completedAt = new DateTimeOffset(2026, 7, 1, 10, 30, 0, TimeSpan.Zero);
        var task = CreateInProgressTask();

        task.Complete(completedAt);

        Assert.Equal(TaskItemStatus.Completed, task.Status);
        Assert.Equal(completedAt, task.CompletedAt);
    }

    [Fact]
    public void Complete_WhenTaskIsReady_ThrowsDomainRuleException()
    {
        var task = CreateReadyTask();

        var exception = Assert.Throws<DomainRuleException>(
            () => task.Complete(DateTimeOffset.UtcNow));

        Assert.Equal("Only an in-progress task can be completed.", exception.Message);
    }

    [Fact]
    public void Reopen_WhenTaskIsCompleted_ReturnsTaskToReady()
    {
        var task = CreateInProgressTask();
        task.Complete(DateTimeOffset.UtcNow);

        task.Reopen();

        Assert.Equal(TaskItemStatus.Ready, task.Status);
        Assert.Null(task.CompletedAt);
    }

    private static TaskItem CreateTask() =>
        TaskItem.Create(TaskId, "Prepare release notes");

    private static TaskItem CreateReadyTask()
    {
        var task = CreateTask();
        task.MoveToReady();
        return task;
    }

    private static TaskItem CreateInProgressTask()
    {
        var task = CreateReadyTask();
        task.Start();
        return task;
    }
}
