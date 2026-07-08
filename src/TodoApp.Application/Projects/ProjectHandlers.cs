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
        try
        {
            var project = Project.Create(
                identifiers.NewId(),
                command.Name,
                command.Description);

            if (command.TargetDate.HasValue)
            {
                project.SetTargetDate(
                    DueDate.Create(command.TargetDate.Value));
            }

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
                .ToArray());
}

public sealed class UpdateProjectHandler(
    IProjectRepository projects,
    IUnitOfWork unitOfWork)
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

        try
        {
            project.Rename(command.Name);
            project.UpdateDescription(command.Description);

            if (command.TargetDate.HasValue)
            {
                project.SetTargetDate(
                    DueDate.Create(command.TargetDate.Value));
            }

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
    IUnitOfWork unitOfWork,
    IClock clock)
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

        try
        {
            var project = Project.Create(
                identifiers.NewId(),
                command.Name,
                command.Description,
                command.WorkspaceId);

            if (command.TargetDate.HasValue)
            {
                project.SetTargetDate(
                    DueDate.Create(command.TargetDate.Value));
            }

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
