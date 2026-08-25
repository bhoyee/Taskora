using TodoApp.Application.Abstractions;
using TodoApp.Application.Notifications;
using TodoApp.Application.Todos;
using TodoApp.Domain.Todos;

namespace TodoApp.Application.Tests.Todos;

// Covers ListPersonalTodosHandler's carry-over behavior: listing a future
// date must leave stale todos untouched, while listing today's date must
// roll incomplete overdue todos forward and notify their owner by email.
public sealed class PersonalTodoHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_DoesNotCarryOverTodosWhenViewingFutureDate()
    {
        var userId = Guid.NewGuid();
        var oldTodo = PersonalTodo.Create(
            Guid.NewGuid(),
            userId,
            "Finish daily checklist",
            new DateOnly(2026, 7, 18),
            null,
            Now);
        var repository = new StubPersonalTodoRepository([oldTodo]);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new ListPersonalTodosHandler(
            repository,
            unitOfWork,
            new StubClock(Now),
            new StubBusinessDateProvider(new DateOnly(2026, 7, 18)),
            new RecordingEmailSender(),
            CreateRoutineGenerator(repository, unitOfWork),
            new StubCurrentUser(userId));

        var result = await handler.HandleAsync(
            new ListPersonalTodosQuery(
                new DateOnly(2026, 7, 19),
                null,
                1,
                10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 7, 18), oldTodo.TodoDate);
        Assert.Equal(0, repository.UserCarryOverLookupCount);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    // Querying with today's date (the default when no explicit date is
    // passed) should trigger carry-over of yesterday's incomplete todo and
    // email its owner.
    [Fact]
    public async Task List_SendsCarryOverEmailWhenTodayFallbackMovesTodos()
    {
        var userId = Guid.NewGuid();
        var oldTodo = PersonalTodo.Create(
            Guid.NewGuid(),
            userId,
            "Finish yesterday follow-up",
            new DateOnly(2026, 7, 17),
            null,
            Now.AddDays(-1));
        var repository = new StubPersonalTodoRepository(
            [oldTodo],
            [
                new PersonalTodoOwner(
                    userId,
                    "Jadesola Aliu",
                    "jadesola@example.com")
            ]);
        var unitOfWork = new RecordingUnitOfWork();
        var emailSender = new RecordingEmailSender();
        var handler = new ListPersonalTodosHandler(
            repository,
            unitOfWork,
            new StubClock(Now),
            new StubBusinessDateProvider(new DateOnly(2026, 7, 18)),
            emailSender,
            CreateRoutineGenerator(repository, unitOfWork),
            new StubCurrentUser(userId));

        var result = await handler.HandleAsync(
            new ListPersonalTodosQuery(
                new DateOnly(2026, 7, 18),
                null,
                1,
                10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 7, 18), oldTodo.TodoDate);
        Assert.Equal(new DateOnly(2026, 7, 17), oldTodo.CarriedOverFromDate);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Single(emailSender.Messages);
        Assert.Contains(
            "Finish yesterday follow-up",
            emailSender.Messages[0].Body,
            StringComparison.Ordinal);
    }

    // IPersonalTodoRepository fake that serves seeded todos/owners for
    // search and per-user carry-over lookups, tracking how many times the
    // per-user carry-over lookup was invoked.
    private sealed class StubPersonalTodoRepository(
        IReadOnlyList<PersonalTodo> searchTodos,
        IReadOnlyList<PersonalTodoOwner>? owners = null)
        : IPersonalTodoRepository
    {
        public int UserCarryOverLookupCount { get; private set; }

        public Task AddAsync(
            PersonalTodo todo,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<PersonalTodo?> GetByIdAsync(
            Guid todoId,
            CancellationToken cancellationToken) =>
            Task.FromResult<PersonalTodo?>(null);

        public Task<PersonalTodoSearchResult> SearchAsync(
            PersonalTodoSearchCriteria criteria,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PersonalTodoSearchResult(searchTodos, searchTodos.Count));

        public Task<IReadOnlyList<PersonalTodo>> ListIncompleteBeforeAsync(
            Guid userId,
            DateOnly targetDate,
            CancellationToken cancellationToken)
        {
            UserCarryOverLookupCount++;
            return Task.FromResult<IReadOnlyList<PersonalTodo>>(
                searchTodos
                    .Where(todo =>
                        todo.UserId == userId &&
                        !todo.IsCompleted &&
                        todo.TodoDate < targetDate)
                    .ToArray());
        }

        public Task<IReadOnlyList<PersonalTodo>> ListIncompleteBeforeAsync(
            DateOnly targetDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PersonalTodo>>([]);

        public Task<IReadOnlyList<PersonalTodoOwner>> ListOwnersAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PersonalTodoOwner>>(
                owners?.Where(owner => userIds.Contains(owner.UserId)).ToArray() ??
                []);

        public Task<IReadOnlyList<PersonalTodo>> ListForUserBetweenAsync(
            Guid userId,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PersonalTodo>>([]);

        public Task RemoveAsync(
            PersonalTodo todo,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    // IUnitOfWork fake that counts how many times changes were saved.
    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    // INotificationEmailSender fake that records every message sent.
    private sealed class RecordingEmailSender : INotificationEmailSender
    {
        public List<NotificationEmailMessage> Messages { get; } = [];

        public Task SendAsync(
            NotificationEmailMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    // IClock stub returning a fixed point in time for deterministic tests.
    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    // IBusinessDateProvider stub returning a fixed "today" business date.
    private sealed class StubBusinessDateProvider(DateOnly today)
        : IBusinessDateProvider
    {
        public DateOnly Today { get; } = today;

        public string TimeZoneId => "Europe/London";
    }

    // ICurrentUser stub representing a fixed, always-authenticated user.
    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid UserId { get; } = userId;
    }

    // Wires a GenerateDailyRoutineTodosHandler dependency required by
    // ListPersonalTodosHandler, backed by an empty routine repository since
    // these tests aren't exercising routine generation.
    private static GenerateDailyRoutineTodosHandler CreateRoutineGenerator(
        IPersonalTodoRepository todos,
        IUnitOfWork unitOfWork) =>
        new(
            new EmptyDailyRoutineRepository(),
            todos,
            unitOfWork,
            new StubIdentifierGenerator(),
            new StubClock(Now),
            new StubBusinessDateProvider(new DateOnly(2026, 7, 18)));

    // IDailyRoutineRepository fake with no seeded routines, used here only
    // to satisfy the routine-generation handler's dependency.
    private sealed class EmptyDailyRoutineRepository : IDailyRoutineRepository
    {
        public Task AddAsync(
            DailyRoutine routine,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<DailyRoutine?> GetByIdAsync(
            Guid routineId,
            CancellationToken cancellationToken) =>
            Task.FromResult<DailyRoutine?>(null);

        public Task<DailyRoutineSearchResult> SearchAsync(
            Guid userId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DailyRoutineSearchResult([], 0));

        public Task<IReadOnlyList<DailyRoutine>> ListDueForGenerationAsync(
            DateOnly businessDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DailyRoutine>>([]);

        public Task<bool> GeneratedTodoExistsAsync(
            Guid routineId,
            DateOnly businessDate,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task RemoveAsync(
            DailyRoutine routine,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    // IIdentifierGenerator stub producing random identifiers.
    private sealed class StubIdentifierGenerator : IIdentifierGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
