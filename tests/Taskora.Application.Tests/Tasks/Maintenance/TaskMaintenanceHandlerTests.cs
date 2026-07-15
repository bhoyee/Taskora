using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Tasks.Maintenance;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tests.Tasks.Maintenance;

public sealed class TaskMaintenanceHandlerTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid ManagerId = Guid.NewGuid();

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
            context.Projects,
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
                context.UnitOfWork,
                new TestCurrentUser(UserId))
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
                context.Projects,
                context.Workspaces,
                context.UnitOfWork,
                new TestCurrentUser(UserId))
            .HandleAsync(
                new UnblockTaskCommand(task.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.Ready, task.Status);
    }

    [Fact]
    public async Task Unblock_WhenCurrentUserIsWorkspaceManager_ReturnsTaskToReady()
    {
        var task = CreateInProgressTask();
        task.RecordCreator(OtherUserId);
        task.Block("Waiting for design approval");
        var context = CreateContext(task);

        var result = await new UnblockTaskHandler(
                context.Tasks,
                context.Projects,
                context.Workspaces,
                context.UnitOfWork,
                new TestCurrentUser(ManagerId))
            .HandleAsync(
                new UnblockTaskCommand(task.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.Ready, task.Status);
    }

    [Fact]
    public async Task Unblock_WhenCurrentUserIsNeitherOwnerManagerCreatorNorAssignee_ReturnsForbidden()
    {
        var task = CreateInProgressTask();
        task.RecordCreator(Guid.NewGuid());
        task.Block("Waiting for design approval");
        var context = CreateContext(task);

        var result = await new UnblockTaskHandler(
                context.Tasks,
                context.Projects,
                context.Workspaces,
                context.UnitOfWork,
                new TestCurrentUser(OtherUserId))
            .HandleAsync(
                new UnblockTaskCommand(task.Id),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("task.unblock_forbidden", result.Error.Code);
        Assert.Equal(TaskItemStatus.Blocked, task.Status);
        Assert.Equal(0, context.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Resume_WhenTaskIsBlocked_ReturnsTaskToInProgress()
    {
        var task = CreateInProgressTask();
        task.Block("Waiting for design approval");
        var context = CreateContext(task);

        var result = await new ResumeTaskHandler(
                context.Tasks,
                context.UnitOfWork,
                new TestCurrentUser(UserId))
            .HandleAsync(
                new ResumeTaskCommand(task.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Null(task.BlockedReason);
        Assert.Equal(1, context.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Resume_WhenCurrentUserIsNotAssignee_ReturnsForbiddenWithoutSaving()
    {
        var task = CreateInProgressTask();
        task.Block("Waiting for design approval");
        var context = CreateContext(task);

        var result = await new ResumeTaskHandler(
                context.Tasks,
                context.UnitOfWork,
                new TestCurrentUser(OtherUserId))
            .HandleAsync(
                new ResumeTaskCommand(task.Id),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("task.assignee_required", result.Error.Code);
        Assert.Equal(TaskItemStatus.Blocked, task.Status);
        Assert.Equal(0, context.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Block_WhenCurrentUserIsNotAssignee_ReturnsForbiddenWithoutSaving()
    {
        var task = CreateInProgressTask();
        var context = CreateContext(task);

        var result = await new BlockTaskHandler(
                context.Tasks,
                context.UnitOfWork,
                new TestCurrentUser(OtherUserId))
            .HandleAsync(
                new BlockTaskCommand(task.Id, "Waiting for design approval"),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("task.assignee_required", result.Error.Code);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Equal(0, context.UnitOfWork.SaveCount);
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
    public async Task Delete_WhenCurrentUserCreatedTask_RemovesTask()
    {
        var task = CreateTask();
        task.RecordCreator(UserId);
        var context = CreateContext(task);

        var result = await new DeleteTaskHandler(
                context.Tasks,
                context.UnitOfWork,
                new TestCurrentUser(UserId))
            .HandleAsync(
                new DeleteTaskCommand(task.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(context.Tasks.WasRemoved(task.Id));
        Assert.Equal(1, context.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Delete_WhenCurrentUserDidNotCreateTask_ReturnsForbiddenWithoutSaving()
    {
        var task = CreateTask();
        task.RecordCreator(UserId);
        var context = CreateContext(task);

        var result = await new DeleteTaskHandler(
                context.Tasks,
                context.UnitOfWork,
                new TestCurrentUser(OtherUserId))
            .HandleAsync(
                new DeleteTaskCommand(task.Id),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.False(context.Tasks.WasRemoved(task.Id));
        Assert.Equal(0, context.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdatePlanningFactors_WhenValuesAreValid_RecalculatesPriority()
    {
        var task = CreateTask();
        task.RecordCreator(UserId);
        var context = CreateContext(task);

        var result = await new UpdatePlanningFactorsHandler(
                context.Tasks,
                context.UnitOfWork,
                new TestCurrentUser(UserId))
            .HandleAsync(
                new UpdatePlanningFactorsCommand(task.Id, 5, 4, 3, 2),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(14.5m, task.Priority.Value);
    }

    [Fact]
    public async Task UpdatePlanningFactors_WhenUserIsNotCreator_ReturnsForbiddenWithoutSaving()
    {
        var task = CreateTask();
        task.RecordCreator(UserId);
        var context = CreateContext(task);

        var result = await new UpdatePlanningFactorsHandler(
                context.Tasks,
                context.UnitOfWork,
                new TestCurrentUser(OtherUserId))
            .HandleAsync(
                new UpdatePlanningFactorsCommand(task.Id, 5, 4, 3, 2),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("task.planning_forbidden", result.Error.Code);
        Assert.Equal(0, context.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdatePlanningFactors_WhenLegacyTaskHasNoCreator_AllowsPlanningInitialization()
    {
        var task = CreateTask();
        var context = CreateContext(task);

        var result = await new UpdatePlanningFactorsHandler(
                context.Tasks,
                context.UnitOfWork,
                new TestCurrentUser(UserId))
            .HandleAsync(
                new UpdatePlanningFactorsCommand(task.Id, 3, 3, 3, 3),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(task.HasPlanningFactors);
        Assert.Equal(7m, task.Priority.Value);
        Assert.Equal(1, context.UnitOfWork.SaveCount);
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
                context.Projects,
                context.UnitOfWork)
            .HandleAsync(
                new UpdateTaskCommand(task.Id, " ", null, null),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(0, context.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_WhenSprintDoesNotBelongToProject_ReturnsNotFound()
    {
        var task = CreateTask();
        var context = CreateContext(task);

        var result = await new UpdateTaskHandler(
                context.Tasks,
                context.Projects,
                context.UnitOfWork)
            .HandleAsync(
                new UpdateTaskCommand(
                    task.Id,
                    task.Title,
                    task.DueDate?.Value,
                    task.EffortEstimate?.Value,
                    Guid.NewGuid()),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("sprint.not_found", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
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

    private static TestContext CreateContext(params TaskItem[] tasks)
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio delivery", UserId);
        workspace.AddMember(UserId, ManagerId, WorkspaceRole.Manager);
        workspace.AddMember(UserId, OtherUserId, WorkspaceRole.Member);
        var project = Project.Create(ProjectId, "Portfolio launch", workspaceId: WorkspaceId);

        return new(
            new InMemoryTaskRepository(tasks),
            new StubProjectRepository(project),
            new StubWorkspaceRepository(workspace),
            new RecordingUnitOfWork());
    }

    private static TaskItem CreateTask() =>
        TaskItem.Create(Guid.NewGuid(), ProjectId, "Publish portfolio");

    private static TaskItem CreateInProgressTask()
    {
        var task = CreateTask();
        task.Assign(UserId);
        task.MoveToReady();
        task.Start();
        return task;
    }

    private sealed record TestContext(
        InMemoryTaskRepository Tasks,
        StubProjectRepository Projects,
        StubWorkspaceRepository Workspaces,
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

        public Task RemoveAsync(
            TaskItem task,
            CancellationToken cancellationToken)
        {
            _tasks.Remove(task.Id);
            return Task.CompletedTask;
        }

        public bool WasRemoved(Guid taskId) => !_tasks.ContainsKey(taskId);
    }

    private sealed class StubProjectRepository(Project? project)
        : IProjectRepository
    {
        public Task AddAsync(
            Project projectToAdd,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<Project?> GetByIdAsync(
            Guid projectId,
            CancellationToken cancellationToken) =>
            Task.FromResult(project?.Id == projectId ? project : null);

        public Task RemoveAsync(
            Project projectToRemove,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<Project>> ListForWorkspaceAsync(
            Guid workspaceId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Project>>(
                project is not null && project.WorkspaceId == workspaceId
                    ? [project]
                    : []);
    }

    private sealed class StubWorkspaceRepository(Workspace? workspace)
        : IWorkspaceRepository
    {
        public Task AddAsync(
            Workspace workspaceToAdd,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<Workspace?> GetByIdAsync(
            Guid workspaceId,
            CancellationToken cancellationToken) =>
            Task.FromResult(workspace?.Id == workspaceId ? workspace : null);

        public Task<IReadOnlyList<Workspace>> ListForUserAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Workspace>>(
                workspace is not null && workspace.HasMember(userId)
                    ? [workspace]
                    : []);
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

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid UserId { get; } = userId;
    }
}
