using TodoApp.Domain.Common;

namespace TodoApp.Domain.Todos;

/// <summary>
/// A timestamped comment attached to a <see cref="PersonalTodo"/>. Created and owned
/// exclusively through the owning <see cref="PersonalTodo"/> aggregate.
/// </summary>
public sealed class PersonalTodoComment
{
    // Reserved for ORM materialization; domain code must use the parameterized constructor.
    private PersonalTodoComment()
    {
    }

    /// <summary>
    /// Creates a comment. Internal because comments are only ever created via the
    /// owning <see cref="PersonalTodo"/> aggregate, which validates the body length.
    /// </summary>
    internal PersonalTodoComment(
        Guid id,
        Guid todoId,
        string body,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException(
                "Comment identifier is required.");
        }

        if (todoId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Todo identifier is required.");
        }

        var trimmed = body?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainValidationException("Comment body is required.");
        }

        if (trimmed.Length > 2000)
        {
            throw new DomainValidationException(
                "Comment must be 2000 characters or fewer.");
        }

        Id = id;
        TodoId = todoId;
        Body = trimmed;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TodoId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
