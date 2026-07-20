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
        TodoPriority priority,
        Guid? dailyRoutineId,
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
        OriginalTodoDate = todoDate;
        Notes = NormalizeNotes(notes);
        Priority = priority;
        DailyRoutineId = dailyRoutineId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string Title { get; private set; }

    public DateOnly TodoDate { get; private set; }

    public DateOnly OriginalTodoDate { get; private set; }

    public DateOnly? CarriedOverFromDate { get; private set; }

    public bool IsCarriedOver => CarriedOverFromDate.HasValue;

    public string? Notes { get; private set; }

    public TodoPriority Priority { get; private set; }

    public Guid? DailyRoutineId { get; private set; }

    public bool IsGeneratedFromDailyRoutine => DailyRoutineId.HasValue;

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
        new(
            id,
            userId,
            title,
            todoDate,
            notes,
            TodoPriority.Medium,
            null,
            createdAt);

    public static PersonalTodo Create(
        Guid id,
        Guid userId,
        string title,
        DateOnly todoDate,
        string? notes,
        TodoPriority priority,
        DateTimeOffset createdAt) =>
        new(id, userId, title, todoDate, notes, priority, null, createdAt);

    public static PersonalTodo CreateFromDailyRoutine(
        Guid id,
        Guid userId,
        Guid dailyRoutineId,
        string title,
        DateOnly todoDate,
        string? notes,
        TodoPriority priority,
        DateTimeOffset createdAt) =>
        new(
            id,
            userId,
            title,
            todoDate,
            notes,
            priority,
            dailyRoutineId,
            createdAt);

    public void Update(
        string title,
        DateOnly todoDate,
        string? notes,
        TodoPriority priority,
        DateTimeOffset updatedAt)
    {
        Title = NormalizeTitle(title);
        TodoDate = todoDate;
        if (todoDate <= OriginalTodoDate)
        {
            CarriedOverFromDate = null;
        }

        Notes = NormalizeNotes(notes);
        Priority = priority;
        UpdatedAt = updatedAt;
    }

    public void CarryOverTo(DateOnly nextDate, DateTimeOffset carriedAt)
    {
        if (IsCompleted || nextDate <= TodoDate)
        {
            return;
        }

        CarriedOverFromDate ??= TodoDate;
        TodoDate = nextDate;
        UpdatedAt = carriedAt;
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

    internal static string? NormalizeNotes(string? notes)
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
