using TodoApp.Domain.Common;

namespace TodoApp.Domain.Tasks;

/// <summary>
/// A timestamped, authored comment attached to a <see cref="TaskItem"/>. Created and
/// owned exclusively through the <see cref="TaskItem"/> aggregate.
/// </summary>
public sealed class TaskNote
{
    // Reserved for ORM materialization; domain code must use the parameterized constructor.
    private TaskNote()
    {
    }

    /// <summary>
    /// Creates a note. Internal because notes are only ever created via the owning
    /// <see cref="TaskItem"/> aggregate, which validates identifiers and body content.
    /// </summary>
    internal TaskNote(
        Guid id,
        Guid taskId,
        Guid authorId,
        string body,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException(
                "Note identifier is required.");
        }

        if (taskId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Task identifier is required.");
        }

        if (authorId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Note author is required.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainValidationException("Note body is required.");
        }

        Id = id;
        TaskId = taskId;
        AuthorId = authorId;
        Body = body.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TaskId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
