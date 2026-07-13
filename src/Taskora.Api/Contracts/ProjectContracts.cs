namespace TodoApp.Api.Contracts;

public sealed record CreateProjectRequest(
    string Name,
    string? Description,
    DateOnly? TargetDate);

public sealed record UpdateProjectRequest(
    string Name,
    string? Description,
    DateOnly? TargetDate);
