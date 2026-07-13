namespace TodoApp.Api.Contracts;

public sealed record CreateTaskRequest(
    string Title,
    DateOnly? DueDate,
    int? Effort,
    int? BusinessValue,
    int? Urgency,
    int? RiskReduction,
    Guid? SprintId);

public sealed record UpdateTaskRequest(
    string Title,
    DateOnly? DueDate,
    int? Effort,
    Guid? SprintId);

public sealed record BlockTaskRequest(string Reason);

public sealed record UpdatePlanningFactorsRequest(
    int BusinessValue,
    int Urgency,
    int RiskReduction,
    int Effort);

public sealed record AddDependencyRequest(Guid DependencyId);

public sealed record AssignTaskRequest(Guid UserId);
