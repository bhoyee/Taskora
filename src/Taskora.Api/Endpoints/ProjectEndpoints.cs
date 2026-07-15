using TodoApp.Api.Contracts;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Projects;
using TodoApp.Application.Projects.Board;
using TodoApp.Application.Tasks.Metadata;

namespace TodoApp.Api.Endpoints;

internal static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects")
            .WithTags("Projects")
            .RequireAuthorization();

        group.MapPost("/", CreateProjectAsync)
            .WithName("CreateProject")
            .Produces<ProjectDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapGet("/{projectId:guid}", GetProjectAsync)
            .WithName("GetProject")
            .Produces<ProjectDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPut("/{projectId:guid}", UpdateProjectAsync)
            .WithName("UpdateProject")
            .Produces<ProjectDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/{projectId:guid}/archive", ArchiveProjectAsync)
            .WithName("ArchiveProject")
            .Produces<ProjectDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapDelete("/{projectId:guid}", DeleteProjectAsync)
            .WithName("DeleteProject")
            .Produces<bool>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/{projectId:guid}/board", GetBoardAsync)
            .WithName("GetProjectBoard")
            .Produces<ProjectBoardDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{projectId:guid}/categories", CreateCategoryAsync)
            .WithName("CreateProjectCategory")
            .Produces<ProjectCategoryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{projectId:guid}/sprints", CreateSprintAsync)
            .WithName("CreateSprint")
            .Produces<SprintDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPut("/{projectId:guid}/sprints/{sprintId:guid}", UpdateSprintAsync)
            .WithName("UpdateSprint")
            .Produces<SprintDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/{projectId:guid}/sprints/{sprintId:guid}/start", StartSprintAsync)
            .WithName("StartSprint")
            .Produces<SprintDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/{projectId:guid}/sprints/{sprintId:guid}/complete", CompleteSprintAsync)
            .WithName("CompleteSprint")
            .Produces<SprintDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/{projectId:guid}/sprints/{sprintId:guid}/cancel", CancelSprintAsync)
            .WithName("CancelSprint")
            .Produces<SprintDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest request,
        CreateProjectHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateProjectCommand(
                request.Name,
                request.Description,
                request.TargetDate),
            cancellationToken);

        return result.IsSuccess
            ? Results.Created(
                $"/api/v1/projects/{result.Value.Id}",
                result.Value)
            : ApiResult.From(result);
    }

    private static async Task<IResult> GetProjectAsync(
        Guid projectId,
        GetProjectByIdHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new GetProjectByIdQuery(projectId),
            cancellationToken));

    private static async Task<IResult> UpdateProjectAsync(
        Guid projectId,
        UpdateProjectRequest request,
        UpdateProjectHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new UpdateProjectCommand(
                projectId,
                request.Name,
                request.Description,
                request.TargetDate),
            cancellationToken));

    private static async Task<IResult> ArchiveProjectAsync(
        Guid projectId,
        ArchiveProjectHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new ArchiveProjectCommand(projectId),
            cancellationToken));

    private static async Task<IResult> DeleteProjectAsync(
        Guid projectId,
        DeleteProjectHandler handler,
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(
            currentUser.UserId,
            cancellationToken);

        var isSuperAdmin = account is not null &&
            IsSuperAdmin(account.User.Email, configuration);

        return ApiResult.From(await handler.HandleAsync(
            new DeleteProjectCommand(projectId, isSuperAdmin),
            cancellationToken));
    }

    private static async Task<IResult> GetBoardAsync(
        Guid projectId,
        GetProjectBoardHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new GetProjectBoardQuery(projectId),
            cancellationToken));

    private static async Task<IResult> CreateCategoryAsync(
        Guid projectId,
        CreateCategoryRequest request,
        CreateCategoryHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new CreateCategoryCommand(projectId, request.Name),
            cancellationToken));

    private static async Task<IResult> CreateSprintAsync(
        Guid projectId,
        CreateSprintRequest request,
        CreateSprintHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new CreateSprintCommand(
                projectId,
                request.Name,
                request.Goal,
                request.StartDate,
                request.EndDate),
            cancellationToken));

    private static async Task<IResult> UpdateSprintAsync(
        Guid projectId,
        Guid sprintId,
        UpdateSprintRequest request,
        UpdateSprintHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new UpdateSprintCommand(
                projectId,
                sprintId,
                request.Name,
                request.Goal,
                request.StartDate,
                request.EndDate),
            cancellationToken));

    private static async Task<IResult> StartSprintAsync(
        Guid projectId,
        Guid sprintId,
        StartSprintHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new ChangeSprintStatusCommand(projectId, sprintId),
            cancellationToken));

    private static async Task<IResult> CompleteSprintAsync(
        Guid projectId,
        Guid sprintId,
        CompleteSprintHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new ChangeSprintStatusCommand(projectId, sprintId),
            cancellationToken));

    private static async Task<IResult> CancelSprintAsync(
        Guid projectId,
        Guid sprintId,
        CancelSprintHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new ChangeSprintStatusCommand(projectId, sprintId),
            cancellationToken));

    private static bool IsSuperAdmin(
        string email,
        IConfiguration configuration)
    {
        var emails = configuration
            .GetSection("Administration:SuperAdminEmails")
            .Get<string[]>() ?? [];
        var singleEmail = configuration["Administration:SuperAdminEmail"];
        if (!string.IsNullOrWhiteSpace(singleEmail))
        {
            emails = [.. emails, singleEmail];
        }

        return emails.Any(candidate =>
            email.Equals(
                candidate?.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record CreateCategoryRequest(string Name);
