using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Projects;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Projects;

namespace TodoApp.Application.Tests.Projects;

public sealed class ProjectHandlerTests
{
    private static readonly Guid ProjectId =
        Guid.Parse("ad047ea8-2a79-4c2c-a0bb-76fc3f594842");
    private static readonly Guid WorkspaceId =
        Guid.Parse("9a1f876a-5f63-4ef1-bf87-2643f31fb54a");
    private static readonly Guid OwnerId =
        Guid.Parse("2e42f664-b1a8-49a5-8d98-feb3ec0f94c8");
    private static readonly Guid ManagerId =
        Guid.Parse("3a5043e5-41aa-4e73-9a04-1dd6b1570e4d");
    private static readonly Guid MemberId =
        Guid.Parse("5855f8d9-6471-486a-9957-a59c09672d01");

    [Fact]
    public async Task Create_WhenCommandIsValid_PersistsProject()
    {
        var repository = new InMemoryProjectRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new CreateProjectHandler(
            repository,
            unitOfWork,
            new StubIdentifierGenerator(ProjectId));

        var result = await handler.HandleAsync(
            new CreateProjectCommand(
                "  Portfolio launch  ",
                "  Public release  ",
                new DateOnly(2026, 8, 15)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectId, result.Value.Id);
        Assert.Equal("Portfolio launch", result.Value.Name);
        Assert.Equal("Public release", result.Value.Description);
        Assert.Equal(new DateOnly(2026, 8, 15), result.Value.TargetDate);
        Assert.NotNull(repository.AddedProject);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_WhenProjectExists_UpdatesDetails()
    {
        var project = Project.Create(
            ProjectId,
            "Portfolio launch",
            workspaceId: WorkspaceId);
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        var repository = new InMemoryProjectRepository(project);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new UpdateProjectHandler(
            repository,
            new StubWorkspaceRepository(workspace),
            unitOfWork,
            new StubCurrentUser(OwnerId));

        var result = await handler.HandleAsync(
            new UpdateProjectCommand(
                ProjectId,
                "Portfolio release",
                "Public release scope",
                new DateOnly(2026, 9, 1)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Portfolio release", project.Name);
        Assert.Equal("Public release scope", project.Description);
        Assert.Equal(new DateOnly(2026, 9, 1), project.TargetDate?.Value);
    }

    [Fact]
    public async Task Archive_WhenProjectExists_UsesClock()
    {
        var archivedAt =
            new DateTimeOffset(2026, 7, 1, 15, 0, 0, TimeSpan.Zero);
        var project = Project.Create(
            ProjectId,
            "Portfolio launch",
            workspaceId: WorkspaceId);
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new ArchiveProjectHandler(
            new InMemoryProjectRepository(project),
            new StubWorkspaceRepository(workspace),
            unitOfWork,
            new StubClock(archivedAt),
            new StubCurrentUser(OwnerId));

        var result = await handler.HandleAsync(
            new ArchiveProjectCommand(ProjectId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(archivedAt, project.ArchivedAt);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetById_WhenProjectExists_ReturnsDetails()
    {
        var project = Project.Create(
            ProjectId,
            "Portfolio launch",
            "Public release");
        var handler = new GetProjectByIdHandler(
            new InMemoryProjectRepository(project));

        var result = await handler.HandleAsync(
            new GetProjectByIdQuery(ProjectId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectId, result.Value.Id);
        Assert.Equal("Portfolio launch", result.Value.Name);
        Assert.False(result.Value.IsArchived);
    }

    [Fact]
    public async Task Update_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new UpdateProjectHandler(
            new InMemoryProjectRepository(),
            new StubWorkspaceRepository(null),
            unitOfWork,
            new StubCurrentUser(OwnerId));

        var result = await handler.HandleAsync(
            new UpdateProjectCommand(
                ProjectId,
                "Portfolio release",
                null,
                null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("project.not_found", result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_WhenDeliveryDateIsMissing_ReturnsValidation()
    {
        var project = Project.Create(
            ProjectId,
            "Portfolio launch",
            workspaceId: WorkspaceId);
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new UpdateProjectHandler(
            new InMemoryProjectRepository(project),
            new StubWorkspaceRepository(workspace),
            unitOfWork,
            new StubCurrentUser(OwnerId));

        var result = await handler.HandleAsync(
            new UpdateProjectCommand(
                ProjectId,
                "Portfolio release",
                null,
                null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Project delivery date is required.", result.Error.Description);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateWorkspaceProject_WhenCurrentUserIsManager_PersistsProject()
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        workspace.AddMember(OwnerId, ManagerId, WorkspaceRole.Manager);
        var projects = new InMemoryProjectRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new CreateWorkspaceProjectHandler(
            projects,
            new StubWorkspaceRepository(workspace),
            unitOfWork,
            new StubIdentifierGenerator(ProjectId),
            new StubCurrentUser(ManagerId));

        var result = await handler.HandleAsync(
            new CreateWorkspaceProjectCommand(
                WorkspaceId,
                "Client portal",
                "Secure workspace delivery",
                new DateOnly(2026, 10, 15)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectId, result.Value.Id);
        Assert.Equal("Client portal", result.Value.Name);
        Assert.Equal(new DateOnly(2026, 10, 15), result.Value.TargetDate);
        Assert.Equal(WorkspaceId, projects.AddedProject?.WorkspaceId);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateWorkspaceProject_WhenCurrentUserIsMember_ReturnsForbidden()
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        workspace.AddMember(OwnerId, MemberId, WorkspaceRole.Member);
        var projects = new InMemoryProjectRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new CreateWorkspaceProjectHandler(
            projects,
            new StubWorkspaceRepository(workspace),
            unitOfWork,
            new StubIdentifierGenerator(ProjectId),
            new StubCurrentUser(MemberId));

        var result = await handler.HandleAsync(
            new CreateWorkspaceProjectCommand(
                WorkspaceId,
                "Client portal",
                null,
                new DateOnly(2026, 10, 15)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("workspace.forbidden", result.Error.Code);
        Assert.Null(projects.AddedProject);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Delete_WhenCurrentUserIsManager_RemovesProject()
    {
        var project = Project.Create(
            ProjectId,
            "Portfolio launch",
            workspaceId: WorkspaceId);
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        workspace.AddMember(OwnerId, ManagerId, WorkspaceRole.Manager);
        var projects = new InMemoryProjectRepository(project);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new DeleteProjectHandler(
            projects,
            new StubWorkspaceRepository(workspace),
            unitOfWork,
            new StubCurrentUser(ManagerId));

        var result = await handler.HandleAsync(
            new DeleteProjectCommand(ProjectId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(ProjectId, projects.RemovedProject?.Id);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Delete_WhenCurrentUserIsMember_ReturnsForbidden()
    {
        var project = Project.Create(
            ProjectId,
            "Portfolio launch",
            workspaceId: WorkspaceId);
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        workspace.AddMember(OwnerId, MemberId, WorkspaceRole.Member);
        var projects = new InMemoryProjectRepository(project);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new DeleteProjectHandler(
            projects,
            new StubWorkspaceRepository(workspace),
            unitOfWork,
            new StubCurrentUser(MemberId));

        var result = await handler.HandleAsync(
            new DeleteProjectCommand(ProjectId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Null(projects.RemovedProject);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    private sealed class InMemoryProjectRepository(params Project[] projects)
        : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects =
            projects.ToDictionary(project => project.Id);

        public Project? AddedProject { get; private set; }

        public Project? RemovedProject { get; private set; }

        public Task<Project?> GetByIdAsync(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            _projects.TryGetValue(projectId, out var project);
            return Task.FromResult(project);
        }

        public Task AddAsync(
            Project project,
            CancellationToken cancellationToken)
        {
            AddedProject = project;
            _projects.Add(project.Id, project);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            Project project,
            CancellationToken cancellationToken)
        {
            RemovedProject = project;
            _projects.Remove(project.Id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Project>> ListForWorkspaceAsync(
            Guid workspaceId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Project>>(
                _projects.Values
                    .Where(project => project.WorkspaceId == workspaceId)
                    .ToArray());
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

        public Task RemoveAsync(
            Workspace workspaceToRemove,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<Workspace>> ListForUserAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Workspace>>(
                workspace is not null && workspace.HasMember(userId)
                    ? [workspace]
                    : []);
    }

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
