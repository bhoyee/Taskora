using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks.Events;

namespace TodoApp.Domain.Tasks;

public sealed class TaskItem
{
    private readonly List<TaskItem> _dependencies = [];
    private readonly List<IDomainEvent> _domainEvents = [];
    private PlanningFactors? _planningFactors;
    private PriorityScore? _priority;

    private TaskItem(Guid id, Guid projectId, string title)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Task identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainValidationException("Task title is required.");
        }

        Id = id;
        if (projectId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Project identifier is required.");
        }

        ProjectId = projectId;
        Title = title.Trim();
        Status = TaskItemStatus.Backlog;
    }

    public Guid Id { get; }

    public Guid ProjectId { get; }

    public string Title { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public string? BlockedReason { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DueDate? DueDate { get; private set; }

    public EffortEstimate? EffortEstimate { get; private set; }

    public PlanningFactors PlanningFactors =>
        _planningFactors ??
        throw new DomainRuleException("Task planning factors have not been set.");

    public PriorityScore Priority =>
        _priority ??
        throw new DomainRuleException("Task planning factors have not been set.");

    public bool HasPlanningFactors => _planningFactors is not null;

    public IReadOnlyCollection<Guid> DependencyIds =>
        _dependencies.Select(dependency => dependency.Id).ToArray();

    public bool HasIncompleteDependencies =>
        _dependencies.Any(dependency => dependency.Status != TaskItemStatus.Completed);

    public bool IsBlocked =>
        Status == TaskItemStatus.Blocked || HasIncompleteDependencies;

    public IReadOnlyCollection<IDomainEvent> DomainEvents =>
        _domainEvents.AsReadOnly();

    public static TaskItem Create(Guid id, Guid projectId, string title) =>
        new(id, projectId, title);

    public void MoveToReady()
    {
        EnsureStatus(
            TaskItemStatus.Backlog,
            "Only a backlog task can be moved to ready.");

        ChangeStatus(TaskItemStatus.Ready);
    }

    public void Start()
    {
        EnsureStatus(
            TaskItemStatus.Ready,
            "Only a ready task can be started.");

        if (HasIncompleteDependencies)
        {
            throw new DomainRuleException(
                "Task cannot start until all dependencies are completed.");
        }

        ChangeStatus(TaskItemStatus.InProgress);
    }

    public void AddDependency(TaskItem dependency)
    {
        if (dependency.Id == Id)
        {
            throw new DomainRuleException("A task cannot depend on itself.");
        }

        if (_dependencies.Any(existing => existing.Id == dependency.Id))
        {
            throw new DomainRuleException("The task dependency already exists.");
        }

        if (dependency.DependsOn(Id, []))
        {
            throw new DomainRuleException(
                "A circular task dependency is not allowed.");
        }

        _dependencies.Add(dependency);
    }

    public void RemoveDependency(Guid dependencyId)
    {
        var dependency = _dependencies.Find(item => item.Id == dependencyId);

        if (dependency is null)
        {
            throw new DomainRuleException("The task dependency does not exist.");
        }

        _dependencies.Remove(dependency);
    }

    public void SetPlanningFactors(PlanningFactors factors)
    {
        _planningFactors = factors;
        _priority = PriorityScore.Calculate(factors);
    }

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainValidationException("Task title is required.");
        }

        Title = title.Trim();
    }

    public void Schedule(DueDate dueDate)
    {
        DueDate = dueDate;
    }

    public void Estimate(EffortEstimate effortEstimate)
    {
        EffortEstimate = effortEstimate;
    }

    public void Block(string reason)
    {
        EnsureStatus(
            TaskItemStatus.InProgress,
            "Only an in-progress task can be blocked.");

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("A blocked reason is required.");
        }

        BlockedReason = reason.Trim();
        ChangeStatus(TaskItemStatus.Blocked);
    }

    public void Unblock()
    {
        EnsureStatus(
            TaskItemStatus.Blocked,
            "Only a blocked task can be unblocked.");

        BlockedReason = null;
        ChangeStatus(TaskItemStatus.Ready);
    }

    public void Complete(DateTimeOffset completedAt)
    {
        EnsureStatus(
            TaskItemStatus.InProgress,
            "Only an in-progress task can be completed.");

        CompletedAt = completedAt;
        ChangeStatus(TaskItemStatus.Completed);
    }

    public void Reopen()
    {
        EnsureStatus(
            TaskItemStatus.Completed,
            "Only a completed task can be reopened.");

        CompletedAt = null;
        ChangeStatus(TaskItemStatus.Ready);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    private void EnsureStatus(TaskItemStatus requiredStatus, string message)
    {
        if (Status != requiredStatus)
        {
            throw new DomainRuleException(message);
        }
    }

    private bool DependsOn(Guid taskId, HashSet<Guid> visited)
    {
        if (Id == taskId)
        {
            return true;
        }

        if (!visited.Add(Id))
        {
            return false;
        }

        return _dependencies.Any(dependency => dependency.DependsOn(taskId, visited));
    }

    private void ChangeStatus(TaskItemStatus newStatus)
    {
        var previousStatus = Status;
        Status = newStatus;
        _domainEvents.Add(
            new TaskStatusChangedDomainEvent(Id, previousStatus, newStatus));
    }
}
