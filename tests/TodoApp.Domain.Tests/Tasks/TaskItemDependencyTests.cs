using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Domain.Tests.Tasks;

public sealed class TaskItemDependencyTests
{
    [Fact]
    public void AddDependency_WhenDependencyIsValid_RecordsDependency()
    {
        var task = CreateTask("Publish release");
        var dependency = CreateTask("Complete security review");

        task.AddDependency(dependency);

        Assert.Contains(dependency.Id, task.DependencyIds);
        Assert.True(task.HasIncompleteDependencies);
        Assert.True(task.IsBlocked);
    }

    [Fact]
    public void AddDependency_WhenTaskDependsOnItself_ThrowsDomainRuleException()
    {
        var task = CreateTask("Publish release");

        var exception = Assert.Throws<DomainRuleException>(
            () => task.AddDependency(task));

        Assert.Equal("A task cannot depend on itself.", exception.Message);
    }

    [Fact]
    public void AddDependency_WhenDependencyAlreadyExists_ThrowsDomainRuleException()
    {
        var task = CreateTask("Publish release");
        var dependency = CreateTask("Complete security review");
        task.AddDependency(dependency);

        var exception = Assert.Throws<DomainRuleException>(
            () => task.AddDependency(dependency));

        Assert.Equal("The task dependency already exists.", exception.Message);
    }

    [Fact]
    public void AddDependency_WhenItCreatesDirectCycle_ThrowsDomainRuleException()
    {
        var first = CreateTask("Prepare release");
        var second = CreateTask("Approve release");
        first.AddDependency(second);

        var exception = Assert.Throws<DomainRuleException>(
            () => second.AddDependency(first));

        Assert.Equal("A circular task dependency is not allowed.", exception.Message);
    }

    [Fact]
    public void AddDependency_WhenItCreatesTransitiveCycle_ThrowsDomainRuleException()
    {
        var first = CreateTask("Prepare release");
        var second = CreateTask("Approve release");
        var third = CreateTask("Publish release");
        first.AddDependency(second);
        second.AddDependency(third);

        var exception = Assert.Throws<DomainRuleException>(
            () => third.AddDependency(first));

        Assert.Equal("A circular task dependency is not allowed.", exception.Message);
    }

    [Fact]
    public void Start_WhenDependencyIsIncomplete_ThrowsDomainRuleException()
    {
        var task = CreateReadyTask("Publish release");
        var dependency = CreateTask("Complete security review");
        task.AddDependency(dependency);

        var exception = Assert.Throws<DomainRuleException>(task.Start);

        Assert.Equal(
            "Task cannot start until all dependencies are completed.",
            exception.Message);
        Assert.Equal(TaskItemStatus.Ready, task.Status);
    }

    [Fact]
    public void Start_WhenAllDependenciesAreCompleted_StartsTask()
    {
        var task = CreateReadyTask("Publish release");
        var dependency = CreateReadyTask("Complete security review");
        dependency.Start();
        dependency.Complete(DateTimeOffset.UtcNow);
        task.AddDependency(dependency);

        task.Start();

        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.False(task.HasIncompleteDependencies);
        Assert.False(task.IsBlocked);
    }

    [Fact]
    public void RemoveDependency_WhenDependencyExists_UnblocksTask()
    {
        var task = CreateTask("Publish release");
        var dependency = CreateTask("Complete security review");
        task.AddDependency(dependency);

        task.RemoveDependency(dependency.Id);

        Assert.Empty(task.DependencyIds);
        Assert.False(task.IsBlocked);
    }

    private static TaskItem CreateTask(string title) =>
        TaskItem.Create(Guid.NewGuid(), title);

    private static TaskItem CreateReadyTask(string title)
    {
        var task = CreateTask(title);
        task.MoveToReady();
        return task;
    }
}
