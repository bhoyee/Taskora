using TodoApp.Domain.Common;
using TodoApp.Domain.Todos;

namespace TodoApp.Domain.Tests.Todos;

public sealed class PersonalTodoTests
{
    private static readonly Guid TodoId =
        Guid.Parse("b4941363-bc74-48ac-8de5-03e09e965186");
    private static readonly Guid UserId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now =
        new(2026, 7, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WhenValuesAreValid_RecordsDailyTodo()
    {
        var todo = PersonalTodo.Create(
            TodoId,
            UserId,
            "  Review deployment checklist  ",
            new DateOnly(2026, 7, 13),
            "  Before lunch  ",
            Now);

        Assert.Equal("Review deployment checklist", todo.Title);
        Assert.Equal("Before lunch", todo.Notes);
        Assert.Equal(new DateOnly(2026, 7, 13), todo.TodoDate);
        Assert.False(todo.IsCompleted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenTitleIsBlank_ThrowsValidationError(string title)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => PersonalTodo.Create(
                TodoId,
                UserId,
                title,
                new DateOnly(2026, 7, 13),
                null,
                Now));

        Assert.Equal("Todo title is required.", exception.Message);
    }

    [Fact]
    public void Complete_ThenReopen_UpdatesState()
    {
        var todo = PersonalTodo.Create(
            TodoId,
            UserId,
            "Review PR",
            new DateOnly(2026, 7, 13),
            null,
            Now);

        todo.Complete(Now.AddHours(1));
        todo.Reopen(Now.AddHours(2));

        Assert.False(todo.IsCompleted);
        Assert.Null(todo.CompletedAt);
        Assert.Equal(Now.AddHours(2), todo.UpdatedAt);
    }

    [Fact]
    public void CarryOverTo_WhenTodoIsIncomplete_MovesTodoAndKeepsOriginalDate()
    {
        var todo = PersonalTodo.Create(
            TodoId,
            UserId,
            "Review PR",
            new DateOnly(2026, 7, 13),
            null,
            Now);

        todo.CarryOverTo(new DateOnly(2026, 7, 14), Now.AddDays(1));

        Assert.Equal(new DateOnly(2026, 7, 14), todo.TodoDate);
        Assert.Equal(new DateOnly(2026, 7, 13), todo.OriginalTodoDate);
        Assert.Equal(new DateOnly(2026, 7, 13), todo.CarriedOverFromDate);
        Assert.True(todo.IsCarriedOver);
    }
}
