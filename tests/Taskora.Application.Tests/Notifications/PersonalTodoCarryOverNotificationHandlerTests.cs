using TodoApp.Application.Abstractions;
using TodoApp.Application.Notifications;
using TodoApp.Application.Todos;
using TodoApp.Domain.Todos;

namespace TodoApp.Application.Tests.Notifications;

// Covers SendPersonalTodoCarryOverNotificationsHandler, which rolls overdue
// personal todos forward to the current business date and emails their
// owners a summary of what carried over.
public sealed class PersonalTodoCarryOverNotificationHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 18, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_CarriesOverIncompleteTodosAndEmailsOwners()
    {
        var userId = Guid.NewGuid();
        var oldTodo = PersonalTodo.Create(
            Guid.NewGuid(),
            userId,
            "Review sprint notes",
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
        var emailSender = new RecordingEmailSender();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new SendPersonalTodoCarryOverNotificationsHandler(
            repository,
            new GenerateDailyRoutineTodosHandler(
                new EmptyDailyRoutineRepository(),
                repository,
                unitOfWork,
                new StubIdentifierGenerator(),
                new StubClock(Now),
                new StubBusinessDateProvider(new DateOnly(2026, 7, 18))),
            emailSender,
            unitOfWork,
            new StubClock(Now),
            new StubBusinessDateProvider(new DateOnly(2026, 7, 18)));

        var result = await handler.HandleAsync(
            new SendPersonalTodoCarryOverNotificationsCommand(),
            CancellationToken.None);

        Assert.Equal(1, result.TodoCarryOverCount);
        Assert.Equal(1, result.UserNotificationCount);
        Assert.Equal(1, result.EmailCount);
        Assert.Equal(new DateOnly(2026, 7, 18), oldTodo.TodoDate);
        Assert.Equal(new DateOnly(2026, 7, 17), oldTodo.CarriedOverFromDate);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Single(emailSender.Messages);
        Assert.Contains(
            "Review sprint notes",
            emailSender.Messages[0].Body,
            StringComparison.Ordinal);
    }

    // IPersonalTodoRepository stub that serves a fixed set of overdue todos
    // and their owners; all other members are no-ops/empty results.
    private sealed class StubPersonalTodoRepository(
        IReadOnlyList<PersonalTodo> overdueTodos,
        IReadOnlyList<PersonalTodoOwner> owners)
        : IPersonalTodoRepository
    {
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
            Task.FromResult(new PersonalTodoSearchResult([], 0));

        public Task<IReadOnlyList<PersonalTodo>> ListIncompleteBeforeAsync(
            Guid userId,
            DateOnly targetDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PersonalTodo>>([]);

        public Task<IReadOnlyList<PersonalTodo>> ListIncompleteBeforeAsync(
            DateOnly targetDate,
            CancellationToken cancellationToken) =>
            Task.FromResult(overdueTodos);

        public Task<IReadOnlyList<PersonalTodoOwner>> ListOwnersAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) =>
            Task.FromResult(owners);

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
