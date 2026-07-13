using TodoApp.Domain.Common;

namespace TodoApp.Domain.Todos;

public sealed class PersonalTodo
{
    private PersonalTodo(
        Guid id,
        Guid userId,
        string title,
        DateOnly todoDate,
        string? notes,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException(
                "Todo identifier is required.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Todo owner is required.");
        }

        Id = id;
        UserId = userId;
        Title = NormalizeTitle(title);
        TodoDate = todoDate;
        Notes = NormalizeNotes(notes);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string Title { get; private set; }

    public DateOnly TodoDate { get; private set; }

    public string? Notes { get; private set; }

    public bool IsCompleted { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static PersonalTodo Create(
        Guid id,
        Guid userId,
        string title,
        DateOnly todoDate,
        string? notes,
        DateTimeOffset createdAt) =>
        new(id, userId, title, todoDate, notes, createdAt);

    public void Update(
        string title,
        DateOnly todoDate,
        string? notes,
        DateTimeOffset updatedAt)
    {
        Title = NormalizeTitle(title);
        TodoDate = todoDate;
        Notes = NormalizeNotes(notes);
        UpdatedAt = updatedAt;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        CompletedAt = completedAt;
        UpdatedAt = completedAt;
    }

    public void Reopen(DateTimeOffset reopenedAt)
    {
        if (!IsCompleted)
        {
            return;
        }

        IsCompleted = false;
        CompletedAt = null;
        UpdatedAt = reopenedAt;
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = title.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainValidationException(
                "Todo title is required.");
        }

        if (normalized.Length > 160)
        {
            throw new DomainValidationException(
                "Todo title must be 160 characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var normalized = notes.Trim();
        if (normalized.Length > 1000)
        {
            throw new DomainValidationException(
                "Todo notes must be 1000 characters or fewer.");
        }

        return normalized;
    }
}
