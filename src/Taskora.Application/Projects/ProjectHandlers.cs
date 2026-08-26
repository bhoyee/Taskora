using TodoApp.Application.Abstractions;
using TodoApp.Application.Collaboration;
using TodoApp.Application.Common;
using TodoApp.Application.PublicDemo;
using TodoApp.Application.Tasks.Metadata;
using TodoApp.Domain.Common;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Projects;

/// <summary>Creates a new standalone (non-workspace) project.</summary>
public sealed class CreateProjectHandler(
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers)
{
    /// <summary>
    /// Requires a target delivery date, then creates and persists a new
    /// <see cref="Project"/> with that date. Returns a validation failure if
    /// the date is missing or domain validation fails, otherwise the created
    /// project as a <see cref="ProjectDto"/>.
    /// </summary>
    public async Task<Result<ProjectDto>> HandleAsync(
        CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.TargetDate.HasValue)
        {
            return ValidationFailure("Project delivery date is required.");
        }

        try
        {
            var project = Project.Create(
                identifiers.NewId(),
                command.Name,
                command.Description);

            project.SetTargetDate(
                DueDate.Create(command.TargetDate.Value));

            await projects.AddAsync(project, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ProjectDto>.Success(ToDto(project));
        }
        catch (DomainValidationException exception)
        {
            return ValidationFailure(exception.Message);
        }
    }

    // Builds a validation-typed failure result for this handler.
    private static Result<ProjectDto> ValidationFailure(string description) =>
        Result<ProjectDto>.Failure(
            new ApplicationError(
                "project.validation",
                description,
                ErrorType.Validation));

    // Maps a project aggregate (with its categories and sprints) to its DTO.
    internal static ProjectDto ToDto(Project project) =>
        new(
            project.Id,
            project.Name,
            project.Description,
            project.TargetDate?.Value,
            project.IsArchived,
            project.ArchivedAt,
            project.Categories
                .OrderBy(category => category.Name)
                .Select(category => new ProjectCategoryDto(
                    category.Id,
                    category.ProjectId,
                    category.Name))
                .ToArray(),
            project.Sprints
                .OrderByDescending(sprint => sprint.Status == SprintStatus.Active)
                .ThenBy(sprint => sprint.StartDate)
                .Select(ToSprintDto)
                .ToArray());

    // Maps a sprint entity to its DTO.
    internal static SprintDto ToSprintDto(Sprint sprint) =>
        new(
            sprint.Id,
            sprint.ProjectId,
            sprint.Name,
            sprint.Goal,
            sprint.StartDate,
            sprint.EndDate,
            sprint.Status.ToString(),
            sprint.ClosedAt);
}

/// <summary>Updates an existing project's name, description, and target date.</summary>
public sealed class UpdateProjectHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the project to exist and, for workspace-scoped projects, the
    /// current user to be a workspace owner or manager (via
    /// <see cref="ProjectAccess.RequireManagerAsync"/>). Requires a target
    /// delivery date, then applies the rename/description/date changes and
    /// persists them, translating domain validation/rule violations into
    /// typed failures.
    /// </summary>
    public async Task<Result<ProjectDto>> HandleAsync(
        UpdateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(
            command.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return ProjectNotFound();
        }

        var permission = await ProjectAccess.RequireManagerAsync(
            workspaces,
            currentUser,
            project,
            cancellationToken);
        if (!permission.IsSuccess)
        {
            return Result<ProjectDto>.Failure(permission.Error);
        }

        if (!command.TargetDate.HasValue)
        {
            return Failure(
                "project.validation",
                "Project delivery date is required.",
                ErrorType.Validation);
        }

        try
        {
            project.Rename(command.Name);
            project.UpdateDescription(command.Description);
            project.SetTargetDate(
                DueDate.Create(command.TargetDate.Value));

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ProjectDto>.Success(
                CreateProjectHandler.ToDto(project));
        }
        catch (DomainValidationException exception)
        {
            return Failure(
                "project.validation",
                exception.Message,
                ErrorType.Validation);
        }
        catch (DomainRuleException exception)
        {
            return Failure(
                "project.conflict",
                exception.Message,
                ErrorType.Conflict);
        }
    }

    // Builds the "project not found" failure.
    private static Result<ProjectDto> ProjectNotFound() =>
        Failure(
            "project.not_found",
            "The project was not found.",
            ErrorType.NotFound);

    // Builds a typed failure result for this handler.
    private static Result<ProjectDto> Failure(
        string code,
        string description,
        ErrorType type) =>
        Result<ProjectDto>.Failure(
            new ApplicationError(code, description, type));
}

/// <summary>Archives a project, marking it as no longer active.</summary>
public sealed class ArchiveProjectHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the project to exist and, for workspace-scoped projects, the
    /// current user to be a workspace owner or manager. Archives the project
    /// with the current timestamp, converting a domain rule violation (e.g.
    /// already archived) into a conflict failure.
    /// </summary>
    public async Task<Result<ProjectDto>> HandleAsync(
        ArchiveProjectCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(
            command.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return Result<ProjectDto>.Failure(
                new ApplicationError(
                    "project.not_found",
                    "The project was not found.",
                    ErrorType.NotFound));
        }

        var permission = await ProjectAccess.RequireManagerAsync(
            workspaces,
            currentUser,
            project,
            cancellationToken);
        if (!permission.IsSuccess)
        {
            return Result<ProjectDto>.Failure(permission.Error);
        }

        try
        {
            project.Archive(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ProjectDto>.Success(
                CreateProjectHandler.ToDto(project));
        }
        catch (DomainRuleException exception)
        {
            return Result<ProjectDto>.Failure(
                new ApplicationError(
                    "project.conflict",
                    exception.Message,
                    ErrorType.Conflict));
        }
    }
}

/// <summary>Deletes a project and its associated data.</summary>
public sealed class DeleteProjectHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the project to exist. Unless
    /// <see cref="DeleteProjectCommand.HasAdministrativeBypass"/> is set,
    /// requires the current user to be a workspace owner or manager for
    /// workspace-scoped projects. Removes the project on success.
    /// </summary>
    public async Task<Result<bool>> HandleAsync(
        DeleteProjectCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(
            command.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "project.not_found",
                    "The project was not found.",
                    ErrorType.NotFound));
        }

        if (command.HasAdministrativeBypass &&
            !PublicDemoIdentifiers.AllowsDestructiveBypass(currentUser.UserId, project.WorkspaceId))
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "project.demo_restricted",
                    "The public demo's Super Admin account can't delete other workspaces' projects.",
                    ErrorType.Forbidden));
        }

        if (!command.HasAdministrativeBypass)
        {
            var permission = await ProjectAccess.RequireManagerAsync(
                workspaces,
                currentUser,
                project,
                cancellationToken);
            if (!permission.IsSuccess)
            {
                return Result<bool>.Failure(permission.Error);
            }
        }

        await projects.RemoveAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

/// <summary>Shared authorization checks for project-management operations.</summary>
internal static class ProjectAccess
{
    /// <summary>
    /// Verifies that, for a workspace-scoped project, the current user is an
    /// active (non-suspended-workspace) member with the Owner or Manager
    /// role; returns success unconditionally for projects with no workspace.
    /// Returns <see cref="ErrorType.NotFound"/> if the workspace is missing,
    /// the user is not a member, or the workspace is suspended, and
    /// <see cref="ErrorType.Forbidden"/> if the user's role is only Member.
    /// </summary>
    public static async Task<Result<bool>> RequireManagerAsync(
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        Project project,
        CancellationToken cancellationToken)
    {
        if (project.WorkspaceId == Guid.Empty)
        {
            return Result<bool>.Success(true);
        }

        var workspace = await workspaces.GetByIdAsync(
            project.WorkspaceId,
            cancellationToken);
        if (workspace is null ||
            !workspace.HasMember(currentUser.UserId) ||
            workspace.IsSuspended)
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "workspace.not_found",
                    "The workspace was not found.",
                    ErrorType.NotFound));
        }

        if (workspace.GetRole(currentUser.UserId) ==
            Domain.Collaboration.WorkspaceRole.Member)
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "workspace.forbidden",
                    "Only workspace owners and managers can manage projects.",
                    ErrorType.Forbidden));
        }

        return Result<bool>.Success(true);
    }
}

/// <summary>Retrieves a single project by its identifier.</summary>
public sealed class GetProjectByIdHandler(IProjectRepository projects)
{
    /// <summary>
    /// Returns the project as a <see cref="ProjectDto"/>, or
    /// <see cref="ErrorType.NotFound"/> if no project with the given
    /// identifier exists.
    /// </summary>
    public async Task<Result<ProjectDto>> HandleAsync(
        GetProjectByIdQuery query,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(
            query.ProjectId,
            cancellationToken);

        return project is null
            ? Result<ProjectDto>.Failure(
                new ApplicationError(
                    "project.not_found",
                    "The project was not found.",
                    ErrorType.NotFound))
            : Result<ProjectDto>.Success(
                CreateProjectHandler.ToDto(project));
    }
}

/// <summary>Lists all projects belonging to a workspace.</summary>
public sealed class ListWorkspaceProjectsHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the current user to be an active member of the workspace,
    /// then returns all of that workspace's projects as
    /// <see cref="ProjectDto"/> instances.
    /// </summary>
    public async Task<Result<IReadOnlyList<ProjectDto>>> HandleAsync(
        ListWorkspaceProjectsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await GetWorkspaceMembersHandler.GetWorkspaceAsync(
            workspaces,
            currentUser,
            query.WorkspaceId,
            cancellationToken);
        if (!access.IsSuccess)
        {
            return Result<IReadOnlyList<ProjectDto>>.Failure(access.Error);
        }

        var result = await projects.ListForWorkspaceAsync(
            query.WorkspaceId,
            cancellationToken);
        return Result<IReadOnlyList<ProjectDto>>.Success(
            result.Select(CreateProjectHandler.ToDto).ToArray());
    }
}

/// <summary>Creates a new project scoped to a workspace.</summary>
public sealed class CreateWorkspaceProjectHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the current user to be an active workspace member with the
    /// Owner or Manager role (Members are forbidden from creating projects),
    /// and requires a target delivery date. Creates and persists the new
    /// project on success.
    /// </summary>
    public async Task<Result<ProjectDto>> HandleAsync(
        CreateWorkspaceProjectCommand command,
        CancellationToken cancellationToken)
    {
        var access = await GetWorkspaceMembersHandler.GetWorkspaceAsync(
            workspaces,
            currentUser,
            command.WorkspaceId,
            cancellationToken);
        if (!access.IsSuccess)
        {
            return Result<ProjectDto>.Failure(access.Error);
        }

        if (!command.TargetDate.HasValue)
        {
            return Result<ProjectDto>.Failure(
                new ApplicationError(
                    "project.validation",
                    "Project delivery date is required.",
                    ErrorType.Validation));
        }

        var role = access.Value.GetRole(currentUser.UserId);
        if (role == Domain.Collaboration.WorkspaceRole.Member)
        {
            return Result<ProjectDto>.Failure(
                new ApplicationError(
                    "workspace.forbidden",
                    "Only workspace owners and managers can create projects.",
                    ErrorType.Forbidden));
        }

        try
        {
            var project = Project.Create(
                identifiers.NewId(),
                command.Name,
                command.Description,
                command.WorkspaceId);

            project.SetTargetDate(
                DueDate.Create(command.TargetDate.Value));

            await projects.AddAsync(project, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ProjectDto>.Success(
                CreateProjectHandler.ToDto(project));
        }
        catch (DomainValidationException exception)
        {
            return Result<ProjectDto>.Failure(
                new ApplicationError(
                    "project.validation",
                    exception.Message,
                    ErrorType.Validation));
        }
    }
}

/// <summary>Adds a new sprint to a project.</summary>
public sealed class CreateSprintHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the project to exist and the current user to be a workspace
    /// owner or manager (via <see cref="ProjectAccess.RequireManagerAsync"/>),
    /// then adds and persists a new sprint, translating domain
    /// validation/rule violations into typed failures.
    /// </summary>
    public async Task<Result<SprintDto>> HandleAsync(
        CreateSprintCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(
            command.ProjectId,
            cancellationToken);
        if (project is null)
        {
            return Failure("project.not_found", "The project was not found.", ErrorType.NotFound);
        }

        var permission = await ProjectAccess.RequireManagerAsync(
            workspaces,
            currentUser,
            project,
            cancellationToken);
        if (!permission.IsSuccess)
        {
            return Result<SprintDto>.Failure(permission.Error);
        }

        try
        {
            var sprint = project.AddSprint(
                identifiers.NewId(),
                command.Name,
                command.Goal,
                command.StartDate,
                command.EndDate);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<SprintDto>.Success(CreateProjectHandler.ToSprintDto(sprint));
        }
        catch (DomainValidationException exception)
        {
            return Failure("sprint.validation", exception.Message, ErrorType.Validation);
        }
        catch (DomainRuleException exception)
        {
            return Failure("sprint.conflict", exception.Message, ErrorType.Conflict);
        }
    }

    // Builds a typed failure result for this handler.
    private static Result<SprintDto> Failure(
        string code,
        string description,
        ErrorType type) =>
        Result<SprintDto>.Failure(new ApplicationError(code, description, type));
}

/// <summary>Updates an existing sprint's name, goal, and dates.</summary>
public sealed class UpdateSprintHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the project and sprint to exist and the current user to be a
    /// workspace owner or manager (via
    /// <see cref="SprintAccess.RequireSprintManagerAsync"/>), then applies
    /// the update and persists it.
    /// </summary>
    public async Task<Result<SprintDto>> HandleAsync(
        UpdateSprintCommand command,
        CancellationToken cancellationToken)
    {
        var access = await SprintAccess.RequireSprintManagerAsync(
            command.ProjectId,
            command.SprintId,
            projects,
            workspaces,
            currentUser,
            cancellationToken);
        if (!access.IsSuccess)
        {
            return Result<SprintDto>.Failure(access.Error);
        }

        try
        {
            access.Value.Update(
                command.Name,
                command.Goal,
                command.StartDate,
                command.EndDate);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<SprintDto>.Success(
                CreateProjectHandler.ToSprintDto(access.Value));
        }
        catch (DomainValidationException exception)
        {
            return Failure("sprint.validation", exception.Message, ErrorType.Validation);
        }
        catch (DomainRuleException exception)
        {
            return Failure("sprint.conflict", exception.Message, ErrorType.Conflict);
        }
    }

    // Builds a typed failure result for this handler.
    private static Result<SprintDto> Failure(
        string code,
        string description,
        ErrorType type) =>
        Result<SprintDto>.Failure(new ApplicationError(code, description, type));
}

/// <summary>Transitions a sprint from Planned to Active status.</summary>
public sealed class StartSprintHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the project and sprint to exist and the current user to be a
    /// workspace owner or manager, then starts the sprint, converting a
    /// domain rule violation (e.g. invalid status transition) into a
    /// conflict failure.
    /// </summary>
    public async Task<Result<SprintDto>> HandleAsync(
        ChangeSprintStatusCommand command,
        CancellationToken cancellationToken)
    {
        var access = await SprintAccess.RequireSprintManagerAsync(
            command.ProjectId,
            command.SprintId,
            projects,
            workspaces,
            currentUser,
            cancellationToken);
        if (!access.IsSuccess)
        {
            return Result<SprintDto>.Failure(access.Error);
        }

        try
        {
            access.Value.Start();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<SprintDto>.Success(
                CreateProjectHandler.ToSprintDto(access.Value));
        }
        catch (DomainRuleException exception)
        {
            return Result<SprintDto>.Failure(
                new ApplicationError("sprint.conflict", exception.Message, ErrorType.Conflict));
        }
    }
}

/// <summary>Transitions a sprint to the Completed status.</summary>
public sealed class CompleteSprintHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the project and sprint to exist and the current user to be a
    /// workspace owner or manager, then completes the sprint with the
    /// current timestamp, converting a domain rule violation into a conflict
    /// failure.
    /// </summary>
    public async Task<Result<SprintDto>> HandleAsync(
        ChangeSprintStatusCommand command,
        CancellationToken cancellationToken)
    {
        var access = await SprintAccess.RequireSprintManagerAsync(
            command.ProjectId,
            command.SprintId,
            projects,
            workspaces,
            currentUser,
            cancellationToken);
        if (!access.IsSuccess)
        {
            return Result<SprintDto>.Failure(access.Error);
        }

        try
        {
            access.Value.Complete(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<SprintDto>.Success(
                CreateProjectHandler.ToSprintDto(access.Value));
        }
        catch (DomainRuleException exception)
        {
            return Result<SprintDto>.Failure(
                new ApplicationError("sprint.conflict", exception.Message, ErrorType.Conflict));
        }
    }
}

/// <summary>Transitions a sprint to the Cancelled status.</summary>
public sealed class CancelSprintHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the project and sprint to exist and the current user to be a
    /// workspace owner or manager, then cancels the sprint with the current
    /// timestamp, converting a domain rule violation into a conflict
    /// failure.
    /// </summary>
    public async Task<Result<SprintDto>> HandleAsync(
        ChangeSprintStatusCommand command,
        CancellationToken cancellationToken)
    {
        var access = await SprintAccess.RequireSprintManagerAsync(
            command.ProjectId,
            command.SprintId,
            projects,
            workspaces,
            currentUser,
            cancellationToken);
        if (!access.IsSuccess)
        {
            return Result<SprintDto>.Failure(access.Error);
        }

        try
        {
            access.Value.Cancel(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<SprintDto>.Success(
                CreateProjectHandler.ToSprintDto(access.Value));
        }
        catch (DomainRuleException exception)
        {
            return Result<SprintDto>.Failure(
                new ApplicationError("sprint.conflict", exception.Message, ErrorType.Conflict));
        }
    }
}

/// <summary>Deletes a sprint from a project.</summary>
public sealed class DeleteSprintHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the project to exist. Unless
    /// <see cref="DeleteSprintCommand.HasAdministrativeBypass"/> is set,
    /// requires the current user to be a workspace owner or manager. Removes
    /// the sprint, translating a missing sprint into a not-found failure.
    /// </summary>
    public async Task<Result<bool>> HandleAsync(
        DeleteSprintCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(
            command.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "project.not_found",
                    "The project was not found.",
                    ErrorType.NotFound));
        }

        if (command.HasAdministrativeBypass &&
            !PublicDemoIdentifiers.AllowsDestructiveBypass(currentUser.UserId, project.WorkspaceId))
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "sprint.demo_restricted",
                    "The public demo's Super Admin account can't delete other workspaces' sprints.",
                    ErrorType.Forbidden));
        }

        if (!command.HasAdministrativeBypass)
        {
            var permission = await ProjectAccess.RequireManagerAsync(
                workspaces,
                currentUser,
                project,
                cancellationToken);
            if (!permission.IsSuccess)
            {
                return Result<bool>.Failure(permission.Error);
            }
        }

        try
        {
            project.RemoveSprint(command.SprintId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (DomainRuleException exception)
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "sprint.not_found",
                    exception.Message,
                    ErrorType.NotFound));
        }
    }
}

/// <summary>Shared authorization and lookup checks for sprint-management operations.</summary>
internal static class SprintAccess
{
    /// <summary>
    /// Resolves the parent project (not-found failure if missing), verifies
    /// the current user is a workspace owner or manager for it, then resolves
    /// the sprint within the project (not-found failure if it does not
    /// exist). Returns the sprint on success.
    /// </summary>
    public static async Task<Result<Sprint>> RequireSprintManagerAsync(
        Guid projectId,
        Guid sprintId,
        IProjectRepository projects,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return Result<Sprint>.Failure(
                new ApplicationError("project.not_found", "The project was not found.", ErrorType.NotFound));
        }

        var permission = await ProjectAccess.RequireManagerAsync(
            workspaces,
            currentUser,
            project,
            cancellationToken);
        if (!permission.IsSuccess)
        {
            return Result<Sprint>.Failure(permission.Error);
        }

        try
        {
            return Result<Sprint>.Success(project.GetSprint(sprintId));
        }
        catch (DomainRuleException exception)
        {
            return Result<Sprint>.Failure(
                new ApplicationError("sprint.not_found", exception.Message, ErrorType.NotFound));
        }
    }
}
