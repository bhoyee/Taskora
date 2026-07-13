namespace TodoApp.Api.Contracts;

public sealed record CreateProjectRequest(
    string Name,
    string? Description,
    DateOnly? TargetDate);

public sealed record UpdateProjectRequest(
    string Name,
    string? Description,
    DateOnly? TargetDate);

public sealed record CreateSprintRequest(
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record UpdateSprintRequest(
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate);
