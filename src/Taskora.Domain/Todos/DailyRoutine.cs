using TodoApp.Domain.Common;

namespace TodoApp.Domain.Todos;

public sealed class DailyRoutine
{
    private DailyRoutine(
        Guid id,
        Guid userId,
        string title,
        string? notes,
        TodoPriority priority,
        DateOnly startDate,
        DateOnly? endDate,
        bool isActive,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException(
                "Daily routine identifier is required.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Daily routine owner is required.");
        }

        Id = id;
        UserId = userId;
        Title = NormalizeTitle(title);
        Notes = PersonalTodo.NormalizeNotes(notes);
        Priority = priority;
        StartDate = startDate;
        EndDate = endDate;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        ValidateDateRange();
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string Title { get; private set; }

    public string? Notes { get; private set; }

    public TodoPriority Priority { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public DateOnly? LastGeneratedDate { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DailyRoutine Create(
        Guid id,
        Guid userId,
        string title,
        string? notes,
        TodoPriority priority,
        DateOnly startDate,
        DateOnly? endDate,
        DateTimeOffset createdAt) =>
        new(
            id,
            userId,
            title,
            notes,
            priority,
            startDate,
            endDate,
            isActive: true,
            createdAt);

    public void Update(
        string title,
        string? notes,
        TodoPriority priority,
        DateOnly startDate,
        DateOnly? endDate,
        bool isActive,
        DateTimeOffset updatedAt)
    {
        Title = NormalizeTitle(title);
        Notes = PersonalTodo.NormalizeNotes(notes);
        Priority = priority;
        StartDate = startDate;
        EndDate = endDate;
        IsActive = isActive;
        UpdatedAt = updatedAt;
        ValidateDateRange();
    }

    public bool ShouldGenerateFor(DateOnly businessDate) =>
        IsActive &&
        businessDate >= StartDate &&
        (EndDate is null || businessDate <= EndDate.Value) &&
        LastGeneratedDate != businessDate;

    public PersonalTodo GenerateTodo(
        Guid todoId,
        DateOnly businessDate,
        DateTimeOffset generatedAt)
    {
        if (!ShouldGenerateFor(businessDate))
        {
            throw new DomainValidationException(
                "Daily routine is not eligible to generate a todo for this date.");
        }

        LastGeneratedDate = businessDate;
        UpdatedAt = generatedAt;
        return PersonalTodo.CreateFromDailyRoutine(
            todoId,
            UserId,
            Id,
            Title,
            businessDate,
            Notes,
            Priority,
            generatedAt);
    }

    private void ValidateDateRange()
    {
        if (EndDate.HasValue && EndDate.Value < StartDate)
        {
            throw new DomainValidationException(
                "Daily routine end date cannot be before the start date.");
        }
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = title.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainValidationException(
                "Daily routine title is required.");
        }

        if (normalized.Length > 160)
        {
            throw new DomainValidationException(
                "Daily routine title must be 160 characters or fewer.");
        }

        return normalized;
    }
}
