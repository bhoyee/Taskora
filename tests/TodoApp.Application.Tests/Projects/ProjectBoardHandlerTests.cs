using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Projects.Board;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tests.Projects;

public sealed class ProjectBoardHandlerTests
{
    [Fact]
    public async Task GetBoard_WhenProjectExists_ReturnsDeliverySummary()
    {
        using var cancellation = new CancellationTokenSource();
        var project = Project.Create(Guid.NewGuid(), "Portfolio launch");
        var blockedTask = TaskItem.Create(
            Guid.NewGuid(),
            project.Id,
            "Complete security review");
        blockedTask.SetPlanningFactors(
            PlanningFactors.Create(5, 5, 5, 2));
        var reader = new StubProjectBoardReader(
            new ProjectBoardSnapshot(
                BacklogCount: 4,
                ReadyCount: 3,
                InProgressCount: 2,
                BlockedCount: 1,
                CompletedCount: 8,
                OverdueCount: 2,
                HighPriorityBlockedTasks: [blockedTask]));
        var handler = new GetProjectBoardHandler(
            new StubProjectRepository(project),
            reader);

        var result = await handler.HandleAsync(
            new GetProjectBoardQuery(project.Id),
            cancellation.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(project.Id, result.Value.ProjectId);
        Assert.Equal("Portfolio launch", result.Value.ProjectName);
        Assert.Equal(18, result.Value.TotalTasks);
        Assert.Equal(8, result.Value.CompletedCount);
        Assert.Equal(2, result.Value.OverdueCount);
        Assert.Single(result.Value.HighPriorityBlockedTasks);
        Assert.Equal(
            blockedTask.Priority.Value,
            result.Value.HighPriorityBlockedTasks[0].PriorityScore);
        Assert.Equal(cancellation.Token, reader.ReceivedCancellationToken);
    }

    [Fact]
    public async Task GetBoard_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        var reader = new StubProjectBoardReader(
            new ProjectBoardSnapshot(0, 0, 0, 0, 0, 0, []));
        var handler = new GetProjectBoardHandler(
            new StubProjectRepository(project: null),
            reader);

        var result = await handler.HandleAsync(
            new GetProjectBoardQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.False(reader.WasCalled);
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
    }

    private sealed class StubProjectBoardReader(ProjectBoardSnapshot snapshot)
        : IProjectBoardReadRepository
    {
        public bool WasCalled { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<ProjectBoardSnapshot> GetAsync(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(snapshot);
        }
    }
}
