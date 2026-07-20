using TodoApp.Application.Abstractions;
using TodoApp.Application.Notifications;
using TodoApp.Application.Todos;
using TodoApp.Domain.Todos;

namespace TodoApp.Application.Tests.Notifications;

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

        public Task RemoveAsync(
            PersonalTodo todo,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

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
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class StubBusinessDateProvider(DateOnly today)
        : IBusinessDateProvider
    {
        public DateOnly Today { get; } = today;

        public string TimeZoneId => "Europe/London";
    }

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

    private sealed class StubIdentifierGenerator : IIdentifierGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
