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

public sealed record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly? TargetDate,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    IReadOnlyCollection<ProjectCategoryDto> Categories);
