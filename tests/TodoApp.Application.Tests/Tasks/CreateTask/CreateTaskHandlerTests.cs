using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Tasks.CreateTask;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tests.Tasks.CreateTask;

public sealed class CreateTaskHandlerTests
{
    private static readonly Guid ProjectId =
        Guid.Parse("03b24f74-fbd0-4cbd-bf83-7b9a172c151c");
    private static readonly Guid TaskId =
        Guid.Parse("7dd36a63-22f1-4b39-bc53-4c955f580ad6");
    private static readonly Guid UserId =
        Guid.Parse("81d74ae7-9399-4daa-baf4-aeaee96dcb58");
    private static readonly DateTimeOffset Now =
        new(2026, 7, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenProjectExists_CreatesAndPersistsTask()
    {
        using var cancellation = new CancellationTokenSource();
        var project = Project.Create(ProjectId, "Portfolio launch");
        var projects = new StubProjectRepository(project);
        var tasks = new RecordingTaskRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = CreateHandler(projects, tasks, unitOfWork);
        var command = new CreateTaskCommand(
            ProjectId,
            "  Publish architecture case study  ",
            new DateOnly(2026, 7, 31),
            3,
            BusinessValue: 3,
            Urgency: 3,
            RiskReduction: 3);

        var result = await handler.HandleAsync(command, cancellation.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskId, result.Value.Id);
        Assert.Equal(ProjectId, result.Value.ProjectId);
        Assert.Equal("Publish architecture case study", result.Value.Title);
        Assert.Equal(TaskItemStatus.Backlog, result.Value.Status);
        Assert.Equal(new DateOnly(2026, 7, 31), result.Value.DueDate);
        Assert.Equal(3, result.Value.Effort);
        Assert.NotNull(tasks.AddedTask);
        Assert.Equal(ProjectId, tasks.AddedTask.ProjectId);
        Assert.True(tasks.AddedTask.HasPlanningFactors);
        Assert.Equal(7m, tasks.AddedTask.Priority.Value);
        Assert.Equal(UserId, tasks.AddedTask.CreatedByUserId);
        Assert.Equal(Now, tasks.AddedTask.CreatedAt);
        Assert.Equal(cancellation.Token, tasks.ReceivedCancellationToken);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(cancellation.Token, unitOfWork.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Handle_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        var projects = new StubProjectRepository(project: null);
        var tasks = new RecordingTaskRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = CreateHandler(projects, tasks, unitOfWork);
        var command = new CreateTaskCommand(ProjectId, "Publish portfolio");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("project.not_found", result.Error.Code);
        Assert.Null(tasks.AddedTask);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_WhenProjectIsArchived_ReturnsConflict()
    {
        var project = Project.Create(ProjectId, "Portfolio launch");
        project.Archive(DateTimeOffset.UtcNow);
        var projects = new StubProjectRepository(project);
        var tasks = new RecordingTaskRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = CreateHandler(projects, tasks, unitOfWork);
        var command = new CreateTaskCommand(ProjectId, "Publish portfolio");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(
            "Archived projects cannot accept new tasks.",
            result.Error.Description);
        Assert.Null(tasks.AddedTask);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_WhenTaskTitleIsBlank_ReturnsValidationError()
    {
        var project = Project.Create(ProjectId, "Portfolio launch");
        var handler = CreateHandler(
            new StubProjectRepository(project),
            new RecordingTaskRepository(),
            new RecordingUnitOfWork());
        var command = new CreateTaskCommand(ProjectId, " ");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Task title is required.", result.Error.Description);
    }

    private static CreateTaskHandler CreateHandler(
        IProjectRepository projects,
        ITaskRepository tasks,
        IUnitOfWork unitOfWork) =>
        new(
            projects,
            tasks,
            unitOfWork,
            new StubIdentifierGenerator(TaskId),
            new StubClock(Now),
            new StubCurrentUser(UserId));

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
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

        public Task<IReadOnlyList<Project>> ListForWorkspaceAsync(
            Guid workspaceId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Project>>(
                project is null ? [] : [project]);
    }

    private sealed class RecordingTaskRepository : ITaskRepository
    {
        public TaskItem? AddedTask { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<TaskItem?> GetByIdAsync(
            Guid taskId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                AddedTask?.Id == taskId ? AddedTask : null);

        public Task AddAsync(
            TaskItem task,
            CancellationToken cancellationToken)
        {
            AddedTask = task;
            ReceivedCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            ReceivedCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class StubIdentifierGenerator(Guid identifier)
        : IIdentifierGenerator
    {
        public Guid NewId() => identifier;
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid UserId { get; } = userId;
    }
}
