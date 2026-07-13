namespace TodoApp.Application.Projects;

using TodoApp.Application.Tasks.Metadata;

public sealed record CreateProjectCommand(
    string Name,
    string? Description = null,
    DateOnly? TargetDate = null);

public sealed record UpdateProjectCommand(
    Guid ProjectId,
    string Name,
    string? Description,
    DateOnly? TargetDate);

public sealed record ArchiveProjectCommand(Guid ProjectId);

public sealed record GetProjectByIdQuery(Guid ProjectId);

public sealed record ListWorkspaceProjectsQuery(Guid WorkspaceId);

public sealed record CreateWorkspaceProjectCommand(
    Guid WorkspaceId,
    string Name,
    string? Description = null,
    DateOnly? TargetDate = null);

public sealed record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly? TargetDate,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    IReadOnlyCollection<ProjectCategoryDto> Categories,
    IReadOnlyCollection<SprintDto> Sprints);

public sealed record SprintDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    DateTimeOffset? ClosedAt);

public sealed record CreateSprintCommand(
    Guid ProjectId,
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record UpdateSprintCommand(
    Guid ProjectId,
    Guid SprintId,
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record ChangeSprintStatusCommand(
    Guid ProjectId,
    Guid SprintId);
