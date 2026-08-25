namespace TodoApp.Api.Contracts;

/// <summary>Request body for creating a new project.</summary>
public sealed record CreateProjectRequest(
    string Name,
    string? Description,
    DateOnly? TargetDate);

/// <summary>Request body for updating an existing project's details.</summary>
public sealed record UpdateProjectRequest(
    string Name,
    string? Description,
    DateOnly? TargetDate);

/// <summary>Request body for creating a new sprint within a project.</summary>
public sealed record CreateSprintRequest(
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate);

/// <summary>Request body for updating an existing sprint's details.</summary>
public sealed record UpdateSprintRequest(
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate);
