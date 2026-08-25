using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Tasks.Queries;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tests.Tasks.Queries;

// Covers the read-side task query handlers (search and get-by-id), including
// mapping of priority scoring and deadline health onto the result DTOs.
public sealed class TaskQueryHandlerTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly DateTimeOffset Now =
        new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    // Verifies pagination metadata and per-item mapping, including priority
    // band/explanation for a task with planning factors and null priority
    // fields for one without.
    [Fact]
    public async Task Search_WhenCriteriaAreValid_ReturnsMappedPage()
    {
        using var cancellation = new CancellationTokenSource();
        var first = CreateTask("Critical release", TaskItemStatus.InProgress);
        first.SetPlanningFactors(PlanningFactors.Create(5, 5, 5, 2));
        var second = CreateTask("Prepare notes", TaskItemStatus.Backlog);
        var repository = new RecordingTaskReadRepository(
            [first, second],
            totalCount: 7);
        var handler = new SearchTasksHandler(repository, new StubClock(Now));
        var query = new SearchTasksQuery(
            ProjectId,
            null,
            TaskItemStatus.InProgress,
            IsBlocked: false,
            Search: "release",
            SortBy: TaskSortBy.PriorityDescending,
            PageNumber: 2,
            PageSize: 2);

        var result = await handler.HandleAsync(query, cancellation.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(2, result.Value.PageSize);
        Assert.Equal(4, result.Value.TotalPages);
        Assert.Collection(
            result.Value.Items,
            item =>
            {
                Assert.Equal(first.Id, item.Id);
                Assert.Equal(first.Priority.Value, item.PriorityScore);
                Assert.Equal(PriorityBand.Critical, item.PriorityBand);
                Assert.NotNull(item.PriorityExplanation);
                Assert.Equal(
                    first.Priority.BusinessValueContribution,
                    item.PriorityExplanation.BusinessValueContribution);
                Assert.Equal(DeadlineHealth.Healthy, item.DeadlineHealth);
            },
            item =>
            {
                Assert.Equal(second.Id, item.Id);
                Assert.Null(item.PriorityScore);
            });
        Assert.Equal(ProjectId, repository.ReceivedCriteria?.ProjectId);
        Assert.Null(repository.ReceivedCriteria?.WorkspaceId);
        Assert.Equal("release", repository.ReceivedCriteria?.Search);
        Assert.Equal(cancellation.Token, repository.ReceivedCancellationToken);
    }

    // Boundary cases: page number below 1, page size below 1, and page size
    // above the maximum of 100.
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Search_WhenPaginationIsInvalid_ReturnsValidationError(
        int pageNumber,
        int pageSize)
    {
        var repository = new RecordingTaskReadRepository([], totalCount: 0);
        var handler = new SearchTasksHandler(repository, new StubClock(Now));
        var query = new SearchTasksQuery(
            PageNumber: pageNumber,
            PageSize: pageSize);

        var result = await handler.HandleAsync(
            query,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Null(repository.ReceivedCriteria);
    }

    [Fact]
    public async Task GetById_WhenTaskExists_ReturnsTaskDetails()
    {
        var task = CreateTask("Publish portfolio", TaskItemStatus.InProgress);
        task.Schedule(DueDate.Create(new DateOnly(2026, 7, 31)));
        task.SetPlanningFactors(PlanningFactors.Create(5, 4, 3, 5));
        var repository = new RecordingTaskReadRepository(
            [task],
            totalCount: 1);
        var handler = new GetTaskByIdHandler(repository, new StubClock(Now));

        var result = await handler.HandleAsync(
            new GetTaskByIdQuery(task.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(task.Id, result.Value.Id);
        Assert.Equal(ProjectId, result.Value.ProjectId);
        Assert.Equal(new DateOnly(2026, 7, 31), result.Value.DueDate);
        Assert.Equal(DeadlineHealth.Healthy, result.Value.DeadlineHealth);
        Assert.Equal(task.Priority.Band, result.Value.PriorityBand);
        Assert.Equal(
            task.Priority.UrgencyContribution,
            result.Value.PriorityExplanation?.UrgencyContribution);
    }

    [Fact]
    public async Task GetById_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        var handler = new GetTaskByIdHandler(
            new RecordingTaskReadRepository([], totalCount: 0),
            new StubClock(Now));

        var result = await handler.HandleAsync(
            new GetTaskByIdQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("task.not_found", result.Error.Code);
    }

    // IClock stub returning a fixed point in time for deterministic tests.
    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    // Builds a task and advances it through as many status transitions as
    // needed to reach the requested status.
    private static TaskItem CreateTask(string title, TaskItemStatus status)
    {
        var task = TaskItem.Create(Guid.NewGuid(), ProjectId, title);

        if (status >= TaskItemStatus.Ready)
        {
            task.MoveToReady();
        }

        if (status >= TaskItemStatus.InProgress)
        {
            task.Start();
        }

        return task;
    }

    // ITaskReadRepository fake that serves seeded items and records the
    // criteria/cancellation token it was called with for assertions.
    private sealed class RecordingTaskReadRepository(
        IReadOnlyList<TaskItem> items,
        int totalCount)
        : ITaskReadRepository
    {
        public TaskSearchCriteria? ReceivedCriteria { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<TaskItem?> GetByIdAsync(
            Guid taskId,
            CancellationToken cancellationToken) =>
            Task.FromResult(items.SingleOrDefault(task => task.Id == taskId));

        public Task<TaskSearchResult> SearchAsync(
            TaskSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            ReceivedCriteria = criteria;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(new TaskSearchResult(items, totalCount));
        }
    }
}
