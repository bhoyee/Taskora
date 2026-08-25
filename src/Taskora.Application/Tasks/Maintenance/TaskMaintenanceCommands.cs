namespace TodoApp.Application.Tasks.Maintenance;

/// <summary>
/// Command requesting that a blocked task identified by <see cref="TaskId"/> be moved back to the
/// ready-to-start state.
/// </summary>
public sealed record MoveTaskToReadyCommand(Guid TaskId);

/// <summary>
/// Command requesting an update to a task's editable fields: title, due date, effort estimate, and
/// sprint assignment.
/// </summary>
public sealed record UpdateTaskCommand(
    Guid TaskId,
    string Title,
    DateOnly? DueDate,
    int? Effort,
    Guid? SprintId = null);

/// <summary>
/// Command requesting that the task identified by <see cref="TaskId"/> be blocked, recording
/// <see cref="Reason"/> as the cause.
/// </summary>
public sealed record BlockTaskCommand(Guid TaskId, string Reason);

/// <summary>
/// Command requesting that a blocker be cleared from the task identified by <see cref="TaskId"/>.
/// </summary>
public sealed record UnblockTaskCommand(Guid TaskId);

/// <summary>
/// Command requesting that a paused task identified by <see cref="TaskId"/> resume active work.
/// </summary>
public sealed record ResumeTaskCommand(Guid TaskId);

/// <summary>
/// Command requesting that a completed or closed task identified by <see cref="TaskId"/> be reopened.
/// </summary>
public sealed record ReopenTaskCommand(Guid TaskId);

/// <summary>
/// Command requesting permanent deletion of the task identified by <see cref="TaskId"/>.
/// </summary>
public sealed record DeleteTaskCommand(Guid TaskId);

/// <summary>
/// Command requesting an update to the prioritization inputs (business value, urgency, risk
/// reduction, effort) used to compute a task's planning score.
/// </summary>
public sealed record UpdatePlanningFactorsCommand(
    Guid TaskId,
    int BusinessValue,
    int Urgency,
    int RiskReduction,
    int Effort);

/// <summary>
/// Command requesting that <see cref="DependencyId"/> be removed as a dependency of <see cref="TaskId"/>.
/// </summary>
public sealed record RemoveTaskDependencyCommand(
    Guid TaskId,
    Guid DependencyId);
