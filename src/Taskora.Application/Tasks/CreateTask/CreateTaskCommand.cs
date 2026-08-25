namespace TodoApp.Application.Tasks.CreateTask;

/// <summary>
/// Command requesting creation of a new task within <see cref="ProjectId"/>, with optional
/// scheduling, effort, planning-factor, and sprint-assignment data.
/// </summary>
public sealed record CreateTaskCommand(
    Guid ProjectId,
    string Title,
    DateOnly? DueDate = null,
    int? Effort = null,
    int? BusinessValue = null,
    int? Urgency = null,
    int? RiskReduction = null,
    Guid? SprintId = null);
