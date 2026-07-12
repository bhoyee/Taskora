namespace TodoApp.Application.Tasks.Maintenance;

public sealed record MoveTaskToReadyCommand(Guid TaskId);

public sealed record UpdateTaskCommand(
    Guid TaskId,
    string Title,
    DateOnly? DueDate,
    int? Effort);

public sealed record BlockTaskCommand(Guid TaskId, string Reason);

public sealed record UnblockTaskCommand(Guid TaskId);

public sealed record ReopenTaskCommand(Guid TaskId);

public sealed record DeleteTaskCommand(Guid TaskId);

public sealed record UpdatePlanningFactorsCommand(
    Guid TaskId,
    int BusinessValue,
    int Urgency,
    int RiskReduction,
    int Effort);

public sealed record RemoveTaskDependencyCommand(
    Guid TaskId,
    Guid DependencyId);
