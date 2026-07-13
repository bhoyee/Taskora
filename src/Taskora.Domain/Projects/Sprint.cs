using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Domain.Projects;

public sealed class Sprint
{
    private Sprint(
        Guid id,
        Guid projectId,
        string name,
        string? goal,
        DateOnly startDate,
        DateOnly endDate)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Sprint identifier is required.");
        }

        if (projectId == Guid.Empty)
        {
            throw new DomainValidationException("Project identifier is required.");
        }

        ValidateDates(startDate, endDate);

        Id = id;
        ProjectId = projectId;
        Name = NormalizeName(name);
        Goal = NormalizeGoal(goal);
        StartDate = startDate;
        EndDate = endDate;
        Status = SprintStatus.Planned;
    }

    public Guid Id { get; }

    public Guid ProjectId { get; }

    public string Name { get; private set; }

    public string? Goal { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public SprintStatus Status { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public static Sprint Create(
        Guid id,
        Guid projectId,
        string name,
        string? goal,
        DateOnly startDate,
        DateOnly endDate) =>
        new(id, projectId, name, goal, startDate, endDate);

    public void Update(
        string name,
        string? goal,
        DateOnly startDate,
        DateOnly endDate)
    {
        EnsureEditable();
        ValidateDates(startDate, endDate);
        Name = NormalizeName(name);
        Goal = NormalizeGoal(goal);
        StartDate = startDate;
        EndDate = endDate;
    }

    public void Start()
    {
        EnsureStatus(SprintStatus.Planned, "Only a planned sprint can be started.");
        Status = SprintStatus.Active;
    }

    public void Complete(DateTimeOffset closedAt)
    {
        EnsureStatus(SprintStatus.Active, "Only an active sprint can be completed.");
        Status = SprintStatus.Completed;
        ClosedAt = closedAt;
    }

    public void Cancel(DateTimeOffset closedAt)
    {
        if (Status is SprintStatus.Completed or SprintStatus.Cancelled)
        {
            throw new DomainRuleException("Closed sprints cannot be cancelled.");
        }

        Status = SprintStatus.Cancelled;
        ClosedAt = closedAt;
    }

    public bool Contains(DueDate dueDate) =>
        dueDate.Value >= StartDate && dueDate.Value <= EndDate;

    private void EnsureEditable()
    {
        if (Status != SprintStatus.Planned)
        {
            throw new DomainRuleException(
                "Only planned sprints can be edited.");
        }
    }

    private void EnsureStatus(SprintStatus requiredStatus, string message)
    {
        if (Status != requiredStatus)
        {
            throw new DomainRuleException(message);
        }
    }

    private static void ValidateDates(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new DomainValidationException(
                "Sprint end date cannot be before the start date.");
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Sprint name is required.");
        }

        return name.Trim();
    }

    private static string? NormalizeGoal(string? goal) =>
        string.IsNullOrWhiteSpace(goal) ? null : goal.Trim();
}
