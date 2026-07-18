using TodoApp.Application.Abstractions;
using TodoApp.Application.Todos;
using TodoApp.Domain.Todos;

namespace TodoApp.Application.Tests.Todos;

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

    private sealed class StubPersonalTodoRepository(
        IReadOnlyList<PersonalTodo> searchTodos)
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
            return Task.FromResult<IReadOnlyList<PersonalTodo>>([]);
        }

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
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
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

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid UserId { get; } = userId;
    }
}
