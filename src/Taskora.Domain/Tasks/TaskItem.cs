using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks.Events;

namespace TodoApp.Domain.Tasks;

/// <summary>
/// Aggregate root for a unit of work within a project. Owns its notes and tags,
/// tracks dependencies on other tasks (guarding against cycles and self-dependency),
/// enforces its own status workflow (backlog -> ready -> in progress -> completed,
/// with blocked as a side-branch), and raises a <see cref="Events.TaskStatusChangedDomainEvent"/>
/// on every status transition.
/// </summary>
public sealed class TaskItem
{
    private readonly List<TaskItem> _dependencies = [];
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<TaskNote> _notes = [];
    private readonly List<TaskTag> _tags = [];
    private PlanningFactors? _planningFactors;
    private PriorityScore? _priority;

    private TaskItem(
        Guid id,
        Guid projectId,
        string title,
        DateTimeOffset createdAt)
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
        CreatedAt = createdAt;
        Status = TaskItemStatus.Backlog;
    }

    public Guid Id { get; }

    public Guid ProjectId { get; }

    public string Title { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public string? BlockedReason { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? CategoryId { get; private set; }

    public Guid? SprintId { get; private set; }

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

    /// <summary>
    /// Ids of every task, transitively, that is not yet completed and blocks this task
    /// from proceeding — i.e. the full unresolved portion of the dependency chain.
    /// </summary>
    public IReadOnlyCollection<Guid> IncompleteDependencyChainIds
    {
        get
        {
            var result = new HashSet<Guid>();
            CollectIncompleteDependencies(result);
            return result.ToArray();
        }
    }

    public bool HasIncompleteDependencies =>
        _dependencies.Any(dependency => dependency.Status != TaskItemStatus.Completed);

    /// <summary>True if the task is explicitly blocked, or is implicitly blocked by an incomplete dependency.</summary>
    public bool IsBlocked =>
        Status == TaskItemStatus.Blocked || HasIncompleteDependencies;

    public IReadOnlyCollection<IDomainEvent> DomainEvents =>
        _domainEvents.AsReadOnly();

    public IReadOnlyCollection<TaskNote> Notes => _notes.AsReadOnly();

    public IReadOnlyCollection<TaskTag> Tags => _tags.AsReadOnly();

    /// <summary>Creates a new task in the <see cref="TaskItemStatus.Backlog"/> state.</summary>
    public static TaskItem Create(
        Guid id,
        Guid projectId,
        string title,
        DateTimeOffset? createdAt = null) =>
        new(id, projectId, title, createdAt ?? DateTimeOffset.UnixEpoch);

    /// <summary>
    /// Derives how healthy the task's deadline is "as of" a given date: completed
    /// tasks are always healthy, tasks without a due date are healthy, an overdue
    /// due date is Overdue, and one within 3 days is AtRisk.
    /// </summary>
    public DeadlineHealth GetDeadlineHealth(DateOnly today)
    {
        if (Status == TaskItemStatus.Completed)
        {
            return DeadlineHealth.Completed;
        }

        if (DueDate is null)
        {
            return DeadlineHealth.Healthy;
        }

        var daysRemaining = DueDate.Value.DayNumber - today.DayNumber;
        return daysRemaining switch
        {
            < 0 => DeadlineHealth.Overdue,
            <= 3 => DeadlineHealth.AtRisk,
            _ => DeadlineHealth.Healthy
        };
    }

    /// <summary>Transitions a backlog task to ready.</summary>
    public void MoveToReady()
    {
        EnsureStatus(
            TaskItemStatus.Backlog,
            "Only a backlog task can be moved to ready.");

        ChangeStatus(TaskItemStatus.Ready);
    }

    /// <summary>
    /// Starts a ready task, moving it to in-progress. Blocked because all its
    /// dependencies must already be completed — work cannot begin while blocked
    /// on unfinished prerequisite tasks.
    /// </summary>
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

    /// <summary>
    /// Adds another task as a dependency of this one. Rejects self-dependency, a
    /// duplicate dependency, and any dependency that would introduce a cycle
    /// (detected by walking the candidate's own dependency graph for a path back to this task).
    /// </summary>
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

    /// <summary>Removes a dependency link, throwing if the dependency does not currently exist.</summary>
    public void RemoveDependency(Guid dependencyId)
    {
        var dependency = _dependencies.Find(item => item.Id == dependencyId);

        if (dependency is null)
        {
            throw new DomainRuleException("The task dependency does not exist.");
        }

        _dependencies.Remove(dependency);
    }

    /// <summary>Sets the task's planning inputs and recomputes its derived <see cref="Priority"/> score.</summary>
    public void SetPlanningFactors(PlanningFactors factors)
    {
        _planningFactors = factors;
        _priority = PriorityScore.Calculate(factors);
    }

    /// <summary>Changes the task's title.</summary>
    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainValidationException("Task title is required.");
        }

        Title = title.Trim();
    }

    /// <summary>Sets or changes the task's due date.</summary>
    public void Schedule(DueDate dueDate)
    {
        DueDate = dueDate;
    }

    /// <summary>Sets or changes the task's effort estimate.</summary>
    public void Estimate(EffortEstimate effortEstimate)
    {
        EffortEstimate = effortEstimate;
    }

    /// <summary>Assigns the task to a user.</summary>
    public void Assign(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Assigned user identifier is required.");
        }

        AssignedUserId = userId;
    }

    /// <summary>Clears the task's assignee.</summary>
    public void Unassign() => AssignedUserId = null;

    /// <summary>Records which user created the task. Intended to be set once, at creation time.</summary>
    public void RecordCreator(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Task creator identifier is required.");
        }

        CreatedByUserId = userId;
    }

    /// <summary>
    /// Assigns the task to a project category, or clears it by passing null.
    /// <see cref="Guid.Empty"/> is rejected as an invalid (non-null but meaningless) id.
    /// </summary>
    public void AssignCategory(Guid? categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Category identifier is required.");
        }

        CategoryId = categoryId;
    }

    /// <summary>
    /// Assigns the task to a sprint, or clears it by passing null.
    /// <see cref="Guid.Empty"/> is rejected as an invalid (non-null but meaningless) id.
    /// </summary>
    public void AssignSprint(Guid? sprintId)
    {
        if (sprintId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Sprint identifier is required.");
        }

        SprintId = sprintId;
    }

    /// <summary>Adds a tag to the task, normalizing the name and rejecting duplicates.</summary>
    public void AddTag(string name)
    {
        var normalized = TaskTag.NormalizeName(name);
        if (_tags.Any(tag => tag.Name == normalized))
        {
            throw new DomainRuleException("The task tag already exists.");
        }

        _tags.Add(new TaskTag(Id, normalized));
    }

    /// <summary>Removes a tag by name (normalized the same way as when added), throwing if not found.</summary>
    public void RemoveTag(string name)
    {
        var normalized = TaskTag.NormalizeName(name);
        var tag = _tags.SingleOrDefault(tag => tag.Name == normalized) ??
            throw new DomainRuleException("The task tag was not found.");
        _tags.Remove(tag);
    }

    /// <summary>Adds a new note/comment to the task.</summary>
    public TaskNote AddNote(
        Guid noteId,
        Guid authorId,
        string body,
        DateTimeOffset createdAt)
    {
        var note = new TaskNote(noteId, Id, authorId, body, createdAt);
        _notes.Add(note);
        return note;
    }

    /// <summary>Blocks an in-progress task, recording why.</summary>
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

    /// <summary>Unblocks a blocked task back to ready, clearing the blocked reason.</summary>
    public void Unblock()
    {
        EnsureStatus(
            TaskItemStatus.Blocked,
            "Only a blocked task can be unblocked.");

        BlockedReason = null;
        ChangeStatus(TaskItemStatus.Ready);
    }

    /// <summary>
    /// Resumes a blocked task directly back to in-progress. Like <see cref="Start"/>,
    /// requires all dependencies to already be completed.
    /// </summary>
    public void Resume()
    {
        EnsureStatus(
            TaskItemStatus.Blocked,
            "Only a blocked task can be resumed.");

        if (HasIncompleteDependencies)
        {
            throw new DomainRuleException(
                "Task cannot resume until all dependencies are completed.");
        }

        BlockedReason = null;
        ChangeStatus(TaskItemStatus.InProgress);
    }

    /// <summary>Completes an in-progress task, recording the completion time.</summary>
    public void Complete(DateTimeOffset completedAt)
    {
        EnsureStatus(
            TaskItemStatus.InProgress,
            "Only an in-progress task can be completed.");

        CompletedAt = completedAt;
        ChangeStatus(TaskItemStatus.Completed);
    }

    /// <summary>Reopens a completed task back to ready, clearing its completion time.</summary>
    public void Reopen()
    {
        EnsureStatus(
            TaskItemStatus.Completed,
            "Only a completed task can be reopened.");

        CompletedAt = null;
        ChangeStatus(TaskItemStatus.Ready);
    }

    /// <summary>
    /// Freely moves the task to any target status (e.g. from a board drag-and-drop),
    /// bypassing the individual workflow guards used by the named transition methods.
    /// Manages the side effects that normally accompany a transition: sets/clears
    /// <see cref="CompletedAt"/> when entering/leaving Completed, and sets/clears
    /// <see cref="BlockedReason"/> when entering/leaving Blocked. A no-op if already
    /// at the target status.
    /// </summary>
    public void MoveToStatus(
        TaskItemStatus target,
        DateTimeOffset occurredAt,
        string? blockedReason = null)
    {
        if (target == Status)
        {
            return;
        }

        if (target == TaskItemStatus.Completed)
        {
            CompletedAt = occurredAt;
        }
        else if (Status == TaskItemStatus.Completed)
        {
            CompletedAt = null;
        }

        if (target == TaskItemStatus.Blocked)
        {
            BlockedReason = string.IsNullOrWhiteSpace(blockedReason)
                ? null
                : blockedReason.Trim();
        }
        else if (Status == TaskItemStatus.Blocked)
        {
            BlockedReason = null;
        }

        ChangeStatus(target);
    }

    /// <summary>
    /// Clears the accumulated domain events, typically called by infrastructure after
    /// they have been dispatched.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    // Guards a status transition, requiring the task to currently be in requiredStatus.
    private void EnsureStatus(TaskItemStatus requiredStatus, string message)
    {
        if (Status != requiredStatus)
        {
            throw new DomainRuleException(message);
        }
    }

    // Recursively checks whether this task (transitively) depends on taskId, used by
    // AddDependency to detect and reject cycles before they can be introduced.
    // 'visited' prevents infinite recursion if a cycle already exists.
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

    // Recursively walks the dependency graph collecting ids of every not-yet-completed
    // dependency, direct or transitive.
    private void CollectIncompleteDependencies(HashSet<Guid> result)
    {
        foreach (var dependency in _dependencies.Where(
                     item => item.Status != TaskItemStatus.Completed))
        {
            if (result.Add(dependency.Id))
            {
                dependency.CollectIncompleteDependencies(result);
            }
        }
    }

    // Central status mutator: every transition goes through here so a
    // TaskStatusChangedDomainEvent is always raised consistently.
    private void ChangeStatus(TaskItemStatus newStatus)
    {
        var previousStatus = Status;
        Status = newStatus;
        _domainEvents.Add(
            new TaskStatusChangedDomainEvent(Id, previousStatus, newStatus));
    }
}
