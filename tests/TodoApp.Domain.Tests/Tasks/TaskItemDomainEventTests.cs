using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;
using TodoApp.Domain.Tasks.Events;

namespace TodoApp.Domain.Tests.Tasks;

public sealed class TaskItemDomainEventTests
{
    [Fact]
    public void MoveToReady_WhenTransitionSucceeds_RecordsStatusChangedEvent()
    {
        var task = CreateTask();

        task.MoveToReady();

        var domainEvent =
            Assert.IsType<TaskStatusChangedDomainEvent>(
                Assert.Single(task.DomainEvents));
        Assert.Equal(task.Id, domainEvent.TaskId);
        Assert.Equal(TaskItemStatus.Backlog, domainEvent.PreviousStatus);
        Assert.Equal(TaskItemStatus.Ready, domainEvent.CurrentStatus);
    }

    [Fact]
    public void Complete_WhenTransitionSucceeds_RecordsStatusChangedEvent()
    {
        var task = CreateTask();
        task.MoveToReady();
        task.Start();
        task.ClearDomainEvents();

        task.Complete(DateTimeOffset.UtcNow);

        var domainEvent =
            Assert.IsType<TaskStatusChangedDomainEvent>(
                Assert.Single(task.DomainEvents));
        Assert.Equal(TaskItemStatus.InProgress, domainEvent.PreviousStatus);
        Assert.Equal(TaskItemStatus.Completed, domainEvent.CurrentStatus);
    }

    [Fact]
    public void Start_WhenTransitionFails_DoesNotRecordDomainEvent()
    {
        var task = CreateTask();

        Assert.Throws<DomainRuleException>(task.Start);

        Assert.Empty(task.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_WhenEventsExist_RemovesRecordedEvents()
    {
        var task = CreateTask();
        task.MoveToReady();

        task.ClearDomainEvents();

        Assert.Empty(task.DomainEvents);
    }

    [Fact]
    public void Lifecycle_WhenSeveralTransitionsSucceed_RecordsEachChangeInOrder()
    {
        var task = CreateTask();

        task.MoveToReady();
        task.Start();
        task.Block("Waiting for approval");
        task.Unblock();

        var transitions = task.DomainEvents
            .OfType<TaskStatusChangedDomainEvent>()
            .Select(domainEvent =>
                (domainEvent.PreviousStatus, domainEvent.CurrentStatus))
            .ToArray();

        Assert.Equal(
            [
                (TaskItemStatus.Backlog, TaskItemStatus.Ready),
                (TaskItemStatus.Ready, TaskItemStatus.InProgress),
                (TaskItemStatus.InProgress, TaskItemStatus.Blocked),
                (TaskItemStatus.Blocked, TaskItemStatus.Ready)
            ],
            transitions);
    }

    private static TaskItem CreateTask() =>
        TaskItem.Create(Guid.NewGuid(), "Publish portfolio");
}
