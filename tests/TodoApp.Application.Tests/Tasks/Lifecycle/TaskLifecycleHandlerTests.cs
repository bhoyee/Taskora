using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Tasks.Lifecycle;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tests.Tasks.Lifecycle;

public sealed class TaskLifecycleHandlerTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    [Fact]
    public async Task Start_WhenTaskIsReady_StartsAndSavesTask()
    {
        var task = CreateReadyTask();
        task.Assign(UserId);
        var repository = new InMemoryTaskRepository(task);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new StartTaskHandler(
            repository,
            unitOfWork,
            new TestCurrentUser(UserId));

        var result = await handler.HandleAsync(
            new StartTaskCommand(task.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.InProgress, result.Value);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Start_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        var handler = new StartTaskHandler(
            new InMemoryTaskRepository(),
            new RecordingUnitOfWork(),
            new TestCurrentUser(UserId));

        var result = await handler.HandleAsync(
            new StartTaskCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("task.not_found", result.Error.Code);
    }

    [Fact]
    public async Task Start_WhenTaskIsInBacklog_ReturnsConflict()
    {
        var task = CreateTask();
        task.Assign(UserId);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new StartTaskHandler(
            new InMemoryTaskRepository(task),
            unitOfWork,
            new TestCurrentUser(UserId));

        var result = await handler.HandleAsync(
            new StartTaskCommand(task.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Only a ready task can be started.", result.Error.Description);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Start_WhenTaskIsAssignedToSomeoneElse_ReturnsForbidden()
    {
        var task = CreateReadyTask();
        task.Assign(UserId);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new StartTaskHandler(
            new InMemoryTaskRepository(task),
            unitOfWork,
            new TestCurrentUser(OtherUserId));

        var result = await handler.HandleAsync(
            new StartTaskCommand(task.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("task.assignee_required", result.Error.Code);
        Assert.Equal(TaskItemStatus.Ready, task.Status);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Start_WhenTaskIsUnassigned_ReturnsConflict()
    {
        var task = CreateReadyTask();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new StartTaskHandler(
            new InMemoryTaskRepository(task),
            unitOfWork,
            new TestCurrentUser(UserId));

        var result = await handler.HandleAsync(
            new StartTaskCommand(task.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("task.assignment_required", result.Error.Code);
        Assert.Equal(TaskItemStatus.Ready, task.Status);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Complete_WhenTaskIsInProgress_UsesClockAndSavesTask()
    {
        var completedAt =
            new DateTimeOffset(2026, 7, 1, 14, 30, 0, TimeSpan.Zero);
        var task = CreateReadyTask();
        task.Assign(UserId);
        task.Start();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new CompleteTaskHandler(
            new InMemoryTaskRepository(task),
            unitOfWork,
            new StubClock(completedAt),
            new TestCurrentUser(UserId));

        var result = await handler.HandleAsync(
            new CompleteTaskCommand(task.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.Completed, result.Value);
        Assert.Equal(completedAt, task.CompletedAt);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AddDependency_WhenTasksExist_AddsDependencyAndSaves()
    {
        var task = CreateTask();
        var dependency = CreateTask();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new AddTaskDependencyHandler(
            new InMemoryTaskRepository(task, dependency),
            unitOfWork);

        var result = await handler.HandleAsync(
            new AddTaskDependencyCommand(task.Id, dependency.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(dependency.Id, task.DependencyIds);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AddDependency_WhenDependencyDoesNotExist_ReturnsNotFound()
    {
        var task = CreateTask();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new AddTaskDependencyHandler(
            new InMemoryTaskRepository(task),
            unitOfWork);

        var result = await handler.HandleAsync(
            new AddTaskDependencyCommand(task.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("task.dependency_not_found", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    private static TaskItem CreateTask() =>
        TaskItem.Create(Guid.NewGuid(), ProjectId, "Publish portfolio");

    private static TaskItem CreateReadyTask()
    {
        var task = CreateTask();
        task.MoveToReady();
        return task;
    }

    private sealed class InMemoryTaskRepository(params TaskItem[] tasks)
        : ITaskRepository
    {
        private readonly Dictionary<Guid, TaskItem> _tasks =
            tasks.ToDictionary(task => task.Id);

        public Task AddAsync(
            TaskItem task,
            CancellationToken cancellationToken)
        {
            _tasks.Add(task.Id, task);
            return Task.CompletedTask;
        }

        public Task<TaskItem?> GetByIdAsync(
            Guid taskId,
            CancellationToken cancellationToken)
        {
            _tasks.TryGetValue(taskId, out var task);
            return Task.FromResult(task);
        }

        public Task RemoveAsync(
            TaskItem task,
            CancellationToken cancellationToken)
        {
            _tasks.Remove(task.Id);
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

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid UserId { get; } = userId;
    }
}
