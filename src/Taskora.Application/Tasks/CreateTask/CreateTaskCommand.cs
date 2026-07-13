namespace TodoApp.Application.Tasks.CreateTask;

public sealed record CreateTaskCommand(
    Guid ProjectId,
    string Title,
    DateOnly? DueDate = null,
    int? Effort = null,
    int? BusinessValue = null,
    int? Urgency = null,
    int? RiskReduction = null,
    Guid? SprintId = null);
