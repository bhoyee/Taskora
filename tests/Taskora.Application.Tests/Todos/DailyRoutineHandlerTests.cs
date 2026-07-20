using TodoApp.Application.Abstractions;
using TodoApp.Application.Todos;
using TodoApp.Domain.Todos;

namespace TodoApp.Application.Tests.Todos;

public sealed class DailyRoutineHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now =
        new(2026, 7, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 7, 20);

    [Fact]
    public async Task Create_DefaultWorkflowCreatesActiveHighPriorityRoutine()
    {
        var routines = new RecordingDailyRoutineRepository();
        var handler = new CreateDailyRoutineHandler(
            routines,
            new RecordingUnitOfWork(),
            new SequentialIdentifierGenerator(),
            new StubClock(Now),
            new StubCurrentUser(UserId));

        var result = await handler.HandleAsync(
            new CreateDailyRoutineCommand(
                "Review delivery board",
                "Check blocked work first.",
                TodoPriority.High,
                Today,
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TodoPriority.High, result.Value.Priority);
        Assert.True(result.Value.IsActive);
        Assert.Single(routines.Items);
    }

    [Fact]
    public async Task Generate_CreatesOneTodoAndSkipsDuplicateRoutineDate()
    {
        var routine = DailyRoutine.Create(
            Guid.NewGuid(),
            UserId,
            "Review delivery board",
            null,
            TodoPriority.High,
            Today,
            null,
            Now);
        var routines = new RecordingDailyRoutineRepository([routine]);
        var todos = new RecordingPersonalTodoRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new GenerateDailyRoutineTodosHandler(
            routines,
            todos,
            unitOfWork,
            new SequentialIdentifierGenerator(),
            new StubClock(Now),
            new StubBusinessDateProvider(Today));

        var first = await handler.HandleAsync(
            new GenerateDailyRoutineTodosCommand(Today),
            CancellationToken.None);
        var second = await handler.HandleAsync(
            new GenerateDailyRoutineTodosCommand(Today),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(1, first.Value.GeneratedCount);
        Assert.Equal(0, second.Value.GeneratedCount);
        Assert.Single(todos.Items);
        Assert.Equal(TodoPriority.High, todos.Items[0].Priority);
    }

    private sealed class RecordingDailyRoutineRepository(
        IReadOnlyList<DailyRoutine>? seed = null)
        : IDailyRoutineRepository
    {
        public List<DailyRoutine> Items { get; } = seed?.ToList() ?? [];

        public Task AddAsync(
            DailyRoutine routine,
            CancellationToken cancellationToken)
        {
            Items.Add(routine);
            return Task.CompletedTask;
        }

        public Task<DailyRoutine?> GetByIdAsync(
            Guid routineId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Id == routineId));

        public Task<DailyRoutineSearchResult> SearchAsync(
            Guid userId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DailyRoutineSearchResult(
                Items.Where(item => item.UserId == userId).ToArray(),
                Items.Count(item => item.UserId == userId)));

        public Task<IReadOnlyList<DailyRoutine>> ListDueForGenerationAsync(
            DateOnly businessDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DailyRoutine>>(
                Items
                    .Where(item => item.ShouldGenerateFor(businessDate))
                    .ToArray());

        public Task<bool> GeneratedTodoExistsAsync(
            Guid routineId,
            DateOnly businessDate,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task RemoveAsync(
            DailyRoutine routine,
            CancellationToken cancellationToken)
        {
            Items.Remove(routine);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPersonalTodoRepository : IPersonalTodoRepository
    {
        public List<PersonalTodo> Items { get; } = [];

        public Task AddAsync(
            PersonalTodo todo,
            CancellationToken cancellationToken)
        {
            Items.Add(todo);
            return Task.CompletedTask;
        }

        public Task<PersonalTodo?> GetByIdAsync(
            Guid todoId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Id == todoId));

        public Task<PersonalTodoSearchResult> SearchAsync(
            PersonalTodoSearchCriteria criteria,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PersonalTodoSearchResult(Items, Items.Count));

        public Task<IReadOnlyList<PersonalTodo>> ListIncompleteBeforeAsync(
            Guid userId,
            DateOnly targetDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PersonalTodo>>([]);

        public Task<IReadOnlyList<PersonalTodo>> ListIncompleteBeforeAsync(
            DateOnly targetDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PersonalTodo>>([]);

        public Task<IReadOnlyList<PersonalTodoOwner>> ListOwnersAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PersonalTodoOwner>>([]);

        public Task RemoveAsync(
            PersonalTodo todo,
            CancellationToken cancellationToken)
        {
            Items.Remove(todo);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class SequentialIdentifierGenerator : IIdentifierGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class StubBusinessDateProvider(DateOnly today)
        : IBusinessDateProvider
    {
        public DateOnly Today { get; } = today;

        public string TimeZoneId => "Europe/London";
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid UserId { get; } = userId;
    }
}
