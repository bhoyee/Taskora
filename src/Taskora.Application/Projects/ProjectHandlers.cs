using TodoApp.Application.Abstractions;
using TodoApp.Application.Collaboration;
using TodoApp.Application.Common;
using TodoApp.Application.Tasks.Metadata;
using TodoApp.Domain.Common;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Projects;

public sealed class CreateProjectHandler(
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers)
{
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

    private static Result<ProjectDto> ValidationFailure(string description) =>
        Result<ProjectDto>.Failure(
            new ApplicationError(
                "project.validation",
                description,
                ErrorType.Validation));

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

public sealed class UpdateProjectHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
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

    private static Result<ProjectDto> ProjectNotFound() =>
        Failure(
            "project.not_found",
            "The project was not found.",
            ErrorType.NotFound);

    private static Result<ProjectDto> Failure(
        string code,
        string description,
        ErrorType type) =>
        Result<ProjectDto>.Failure(
            new ApplicationError(code, description, type));
}

public sealed class ArchiveProjectHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
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

public sealed class DeleteProjectHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
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

internal static class ProjectAccess
{
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
            !workspace.HasMember(currentUser.UserId))
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

public sealed class GetProjectByIdHandler(IProjectRepository projects)
{
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

public sealed class ListWorkspaceProjectsHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    ICurrentUser currentUser)
{
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

public sealed class CreateWorkspaceProjectHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    ICurrentUser currentUser)
{
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

public sealed class CreateSprintHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    ICurrentUser currentUser)
{
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

    private static Result<SprintDto> Failure(
        string code,
        string description,
        ErrorType type) =>
        Result<SprintDto>.Failure(new ApplicationError(code, description, type));
}

public sealed class UpdateSprintHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
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

    private static Result<SprintDto> Failure(
        string code,
        string description,
        ErrorType type) =>
        Result<SprintDto>.Failure(new ApplicationError(code, description, type));
}

public sealed class StartSprintHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
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

public sealed class CompleteSprintHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
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

public sealed class CancelSprintHandler(
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
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

internal static class SprintAccess
{
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
