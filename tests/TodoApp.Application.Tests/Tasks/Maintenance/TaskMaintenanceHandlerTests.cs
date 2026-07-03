using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Tasks.Maintenance;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tests.Tasks.Maintenance;

public sealed class TaskMaintenanceHandlerTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    [Fact]
    public async Task MoveToReady_WhenTaskIsInBacklog_ChangesStatus()
    {
        var task = CreateTask();
        var context = CreateContext(task);

        var result = await new MoveTaskToReadyHandler(
                context.Tasks,
                context.UnitOfWork)
            .HandleAsync(
                new MoveTaskToReadyCommand(task.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.Ready, task.Status);
        Assert.Equal(1, context.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_WhenValuesAreValid_UpdatesTaskDetails()
    {
        var task = CreateTask();
        var context = CreateContext(task);
        var handler = new UpdateTaskHandler(
            context.Tasks,
            context.UnitOfWork);

        var result = await handler.HandleAsync(
            new UpdateTaskCommand(
                task.Id,
                "  Publish case study  ",
                new DateOnly(2026, 8, 1),
                5),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Publish case study", task.Title);
        Assert.Equal(new DateOnly(2026, 8, 1), task.DueDate?.Value);
        Assert.Equal(5, task.EffortEstimate?.Value);
    }

    [Fact]
    public async Task Block_WhenTaskIsInProgress_RecordsReason()
    {
        var task = CreateInProgressTask();
        var context = CreateContext(task);

        var result = await new BlockTaskHandler(
                context.Tasks,
                context.UnitOfWork)
            .HandleAsync(
                new BlockTaskCommand(task.Id, "Waiting for design approval"),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.Blocked, task.Status);
        Assert.Equal("Waiting for design approval", task.BlockedReason);
    }

    [Fact]
    public async Task Unblock_WhenTaskIsBlocked_ReturnsTaskToReady()
    {
        var task = CreateInProgressTask();
        task.Block("Waiting for design approval");
        var context = CreateContext(task);

        var result = await new UnblockTaskHandler(
                context.Tasks,
                context.UnitOfWork)
            .HandleAsync(
                new UnblockTaskCommand(task.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.Ready, task.Status);
    }

    [Fact]
    public async Task Reopen_WhenTaskIsCompleted_ReturnsTaskToReady()
    {
        var task = CreateInProgressTask();
        task.Complete(DateTimeOffset.UtcNow);
        var context = CreateContext(task);

        var result = await new ReopenTaskHandler(
                context.Tasks,
                context.UnitOfWork)
            .HandleAsync(
                new ReopenTaskCommand(task.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.Ready, task.Status);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public async Task UpdatePlanningFactors_WhenValuesAreValid_RecalculatesPriority()
    {
        var task = CreateTask();
        var context = CreateContext(task);

        var result = await new UpdatePlanningFactorsHandler(
                context.Tasks,
                context.UnitOfWork)
            .HandleAsync(
                new UpdatePlanningFactorsCommand(task.Id, 5, 4, 3, 2),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(14.5m, task.Priority.Value);
    }

    [Fact]
    public async Task RemoveDependency_WhenDependencyExists_RemovesIt()
    {
        var task = CreateTask();
        var dependency = CreateTask();
        task.AddDependency(dependency);
        var context = CreateContext(task, dependency);

        var result = await new RemoveTaskDependencyHandler(
                context.Tasks,
                context.UnitOfWork)
            .HandleAsync(
                new RemoveTaskDependencyCommand(task.Id, dependency.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(task.DependencyIds);
    }

    [Fact]
    public async Task Update_WhenTitleIsBlank_ReturnsValidationErrorWithoutSaving()
    {
        var task = CreateTask();
        var context = CreateContext(task);

        var result = await new UpdateTaskHandler(
                context.Tasks,
                context.UnitOfWork)
            .HandleAsync(
                new UpdateTaskCommand(task.Id, " ", null, null),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(0, context.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task MoveToReady_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        var context = CreateContext();

        var result = await new MoveTaskToReadyHandler(
                context.Tasks,
                context.UnitOfWork)
            .HandleAsync(
                new MoveTaskToReadyCommand(Guid.NewGuid()),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("task.not_found", result.Error.Code);
    }

    private static TestContext CreateContext(params TaskItem[] tasks) =>
        new(new InMemoryTaskRepository(tasks), new RecordingUnitOfWork());

    private static TaskItem CreateTask() =>
        TaskItem.Create(Guid.NewGuid(), ProjectId, "Publish portfolio");

    private static TaskItem CreateInProgressTask()
    {
        var task = CreateTask();
        task.MoveToReady();
        task.Start();
        return task;
    }

    private sealed record TestContext(
        InMemoryTaskRepository Tasks,
        RecordingUnitOfWork UnitOfWork);

    private sealed class InMemoryTaskRepository(params TaskItem[] tasks)
        : ITaskRepository
    {
        private readonly Dictionary<Guid, TaskItem> _tasks =
            tasks.ToDictionary(task => task.Id);

        public Task<TaskItem?> GetByIdAsync(
            Guid taskId,
            CancellationToken cancellationToken)
        {
            _tasks.TryGetValue(taskId, out var task);
            return Task.FromResult(task);
        }

        public Task AddAsync(
            TaskItem task,
            CancellationToken cancellationToken)
        {
            _tasks.Add(task.Id, task);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
