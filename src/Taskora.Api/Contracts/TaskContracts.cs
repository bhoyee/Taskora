namespace TodoApp.Api.Contracts;

/// <summary>Request body for creating a new task, including optional planning/prioritization factors.</summary>
public sealed record CreateTaskRequest(
    string Title,
    DateOnly? DueDate,
    int? Effort,
    int? BusinessValue,
    int? Urgency,
    int? RiskReduction,
    Guid? SprintId);

/// <summary>Request body for updating a task's core fields.</summary>
public sealed record UpdateTaskRequest(
    string Title,
    DateOnly? DueDate,
    int? Effort,
    Guid? SprintId);

/// <summary>Request body for marking a task as blocked, with a required reason.</summary>
public sealed record BlockTaskRequest(string Reason);

/// <summary>Request body for updating a task's prioritization/planning factors.</summary>
public sealed record UpdatePlanningFactorsRequest(
    int BusinessValue,
    int Urgency,
    int RiskReduction,
    int Effort);

/// <summary>Request body for adding a dependency link to another task.</summary>
public sealed record AddDependencyRequest(Guid DependencyId);

/// <summary>Request body for assigning a task to a user.</summary>
public sealed record AssignTaskRequest(Guid UserId);
