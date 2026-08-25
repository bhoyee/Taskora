using TodoApp.Domain.Common;

namespace TodoApp.Domain.Todos;

/// <summary>
/// Aggregate root describing a recurring personal todo template: a title/notes/priority
/// paired with an active date range, used to generate one <see cref="PersonalTodo"/>
/// instance per eligible calendar day.
/// </summary>
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

    /// <summary>Creates a new, active daily routine.</summary>
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

    /// <summary>Updates the routine's editable fields, including its active flag and date range.</summary>
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

    /// <summary>
    /// True if a todo should be generated for the given date: the routine must be
    /// active, the date must fall within its start/end range, and a todo must not
    /// already have been generated for that exact date (idempotency guard).
    /// </summary>
    public bool ShouldGenerateFor(DateOnly businessDate) =>
        IsActive &&
        businessDate >= StartDate &&
        (EndDate is null || businessDate <= EndDate.Value) &&
        LastGeneratedDate != businessDate;

    /// <summary>
    /// Generates a new <see cref="PersonalTodo"/> for the given date, re-validating
    /// eligibility via <see cref="ShouldGenerateFor"/> and updating
    /// <see cref="LastGeneratedDate"/> to prevent duplicate generation for the same date.
    /// </summary>
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

    // Ensures the optional end date, if set, is not before the start date.
    private void ValidateDateRange()
    {
        if (EndDate.HasValue && EndDate.Value < StartDate)
        {
            throw new DomainValidationException(
                "Daily routine end date cannot be before the start date.");
        }
    }

    // Trims the title and enforces it is present and within the 160-character limit.
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
