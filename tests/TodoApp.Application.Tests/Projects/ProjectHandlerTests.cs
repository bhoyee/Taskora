using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Projects;
using TodoApp.Domain.Projects;

namespace TodoApp.Application.Tests.Projects;

public sealed class ProjectHandlerTests
{
    private static readonly Guid ProjectId =
        Guid.Parse("ad047ea8-2a79-4c2c-a0bb-76fc3f594842");

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
        var project = Project.Create(ProjectId, "Portfolio launch");
        var repository = new InMemoryProjectRepository(project);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new UpdateProjectHandler(repository, unitOfWork);

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
        var project = Project.Create(ProjectId, "Portfolio launch");
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new ArchiveProjectHandler(
            new InMemoryProjectRepository(project),
            unitOfWork,
            new StubClock(archivedAt));

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
            unitOfWork);

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

    private sealed class InMemoryProjectRepository(params Project[] projects)
        : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects =
            projects.ToDictionary(project => project.Id);

        public Project? AddedProject { get; private set; }

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

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
