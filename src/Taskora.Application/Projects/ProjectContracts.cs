namespace TodoApp.Application.Projects;

using TodoApp.Application.Tasks.Metadata;

/// <summary>Command to create a standalone (non-workspace) project.</summary>
public sealed record CreateProjectCommand(
    string Name,
    string? Description = null,
    DateOnly? TargetDate = null);

/// <summary>Command to update a project's name, description, and target date.</summary>
public sealed record UpdateProjectCommand(
    Guid ProjectId,
    string Name,
    string? Description,
    DateOnly? TargetDate);

/// <summary>Command to archive a project.</summary>
public sealed record ArchiveProjectCommand(Guid ProjectId);

/// <summary>Command to delete a project, optionally bypassing membership/role checks for administrative use.</summary>
public sealed record DeleteProjectCommand(
    Guid ProjectId,
    bool HasAdministrativeBypass = false);

/// <summary>Query to fetch a single project by identifier.</summary>
public sealed record GetProjectByIdQuery(Guid ProjectId);

/// <summary>Query to list all projects belonging to a workspace.</summary>
public sealed record ListWorkspaceProjectsQuery(Guid WorkspaceId);

/// <summary>Command to create a project scoped to a workspace.</summary>
public sealed record CreateWorkspaceProjectCommand(
    Guid WorkspaceId,
    string Name,
    string? Description = null,
    DateOnly? TargetDate = null);

/// <summary>Represents a project, including its categories and sprints.</summary>
public sealed record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly? TargetDate,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    IReadOnlyCollection<ProjectCategoryDto> Categories,
    IReadOnlyCollection<SprintDto> Sprints);

/// <summary>Represents a sprint within a project.</summary>
public sealed record SprintDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    DateTimeOffset? ClosedAt);

/// <summary>Command to add a new sprint to a project.</summary>
public sealed record CreateSprintCommand(
    Guid ProjectId,
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate);

/// <summary>Command to update an existing sprint's details.</summary>
public sealed record UpdateSprintCommand(
    Guid ProjectId,
    Guid SprintId,
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate);

/// <summary>Command identifying a sprint whose status should be transitioned (start/complete/cancel).</summary>
public sealed record ChangeSprintStatusCommand(
    Guid ProjectId,
    Guid SprintId);

/// <summary>Command to delete a sprint, optionally bypassing membership/role checks for administrative use.</summary>
public sealed record DeleteSprintCommand(
    Guid ProjectId,
    Guid SprintId,
    bool HasAdministrativeBypass = false);
