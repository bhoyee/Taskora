using TodoApp.Domain.Common;
using TodoApp.Domain.Todos;

namespace TodoApp.Domain.Tests.Todos;

public sealed class DailyRoutineTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GenerateTodo_CreatesHighPriorityTodoForBusinessDate()
    {
        var routine = DailyRoutine.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Review delivery board",
            "Check blocked work first.",
            TodoPriority.High,
            new DateOnly(2026, 7, 20),
            null,
            Now);

        var todo = routine.GenerateTodo(
            Guid.NewGuid(),
            new DateOnly(2026, 7, 20),
            Now.AddMinutes(1));

        Assert.Equal("Review delivery board", todo.Title);
        Assert.Equal(TodoPriority.High, todo.Priority);
        Assert.Equal(new DateOnly(2026, 7, 20), todo.TodoDate);
        Assert.True(todo.IsGeneratedFromDailyRoutine);
        Assert.Equal(routine.Id, todo.DailyRoutineId);
        Assert.Equal(new DateOnly(2026, 7, 20), routine.LastGeneratedDate);
    }

    [Fact]
    public void GenerateTodo_WhenAlreadyGeneratedForDate_ThrowsValidationError()
    {
        var routine = DailyRoutine.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Review delivery board",
            null,
            TodoPriority.High,
            new DateOnly(2026, 7, 20),
            null,
            Now);
        routine.GenerateTodo(
            Guid.NewGuid(),
            new DateOnly(2026, 7, 20),
            Now.AddMinutes(1));

        var exception = Assert.Throws<DomainValidationException>(() =>
            routine.GenerateTodo(
                Guid.NewGuid(),
                new DateOnly(2026, 7, 20),
                Now.AddMinutes(2)));
        Assert.Contains("not eligible", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WhenEndDateIsBeforeStartDate_ThrowsValidationError()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            DailyRoutine.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Review delivery board",
                null,
                TodoPriority.High,
                new DateOnly(2026, 7, 20),
                new DateOnly(2026, 7, 19),
                Now));

        Assert.Contains("end date", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
