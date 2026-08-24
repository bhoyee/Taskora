using TodoApp.Domain.Common;

namespace TodoApp.Domain.Todos;

public sealed class PersonalTodoComment
{
    private PersonalTodoComment()
    {
    }

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
