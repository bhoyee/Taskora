using TodoApp.Api.Authorization;
using TodoApp.Api.Contracts;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Projects;
using TodoApp.Application.Projects.Board;
using TodoApp.Application.Tasks.Metadata;

namespace TodoApp.Api.Endpoints;

/// <summary>
/// Registers project, board, category, and sprint management routes under
/// "/api/v1/projects". Every route requires an authenticated caller;
/// project/sprint deletion additionally check for super-admin privileges
/// (via <see cref="SuperAdminAuthorization.IsSuperAdminAsync"/>) to allow
/// deleting resources the caller doesn't otherwise own.
/// </summary>
internal static class ProjectEndpoints
{
    /// <summary>Maps the project endpoint group; all routes require authentication.</summary>
    public static IEndpointRouteBuilder MapProjectEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects")
            .WithTags("Projects")
            .RequireAuthorization();

        // POST /api/v1/projects/
        // Creates a new project (201 ProjectDto with Location header, 400 on
        // validation failure).
        group.MapPost("/", CreateProjectAsync)
            .WithName("CreateProject")
            .Produces<ProjectDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        // GET /api/v1/projects/{projectId}
        // Fetches a single project by id (200 ProjectDto, 404 if not found).
        group.MapGet("/{projectId:guid}", GetProjectAsync)
            .WithName("GetProject")
            .Produces<ProjectDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        // PUT /api/v1/projects/{projectId}
        // Updates a project's name/description/target date (200 ProjectDto,
        // 400 on validation failure, 404 if not found, 409 on conflicting update).
        group.MapPut("/{projectId:guid}", UpdateProjectAsync)
            .WithName("UpdateProject")
            .Produces<ProjectDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        // POST /api/v1/projects/{projectId}/archive
        // Archives a project (200 ProjectDto, 404 if not found, 409 if it
        // cannot be archived in its current state).
        group.MapPost("/{projectId:guid}/archive", ArchiveProjectAsync)
            .WithName("ArchiveProject")
            .Produces<ProjectDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        // DELETE /api/v1/projects/{projectId}
        // Deletes a project. Callers may delete their own projects; deleting
        // another user's project additionally requires super-admin status
        // (200 bool success flag, 403 if not permitted, 404 if not found).
        group.MapDelete("/{projectId:guid}", DeleteProjectAsync)
            .WithName("DeleteProject")
            .Produces<bool>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        // GET /api/v1/projects/{projectId}/board
        // Fetches the project's Kanban board (200 ProjectBoardDto, 404 if not found).
        group.MapGet("/{projectId:guid}/board", GetBoardAsync)
            .WithName("GetProjectBoard")
            .Produces<ProjectBoardDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        // POST /api/v1/projects/{projectId}/categories
        // Creates a new task category within a project (200
        // ProjectCategoryDto, 400 on validation failure, 404 if the project
        // does not exist).
        group.MapPost("/{projectId:guid}/categories", CreateCategoryAsync)
            .WithName("CreateProjectCategory")
            .Produces<ProjectCategoryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        // POST /api/v1/projects/{projectId}/sprints
        // Creates a new sprint within a project (200 SprintDto, 400 on
        // validation failure, 404 if the project does not exist, 409 on a
        // scheduling conflict with existing sprints).
        group.MapPost("/{projectId:guid}/sprints", CreateSprintAsync)
            .WithName("CreateSprint")
            .Produces<SprintDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        // PUT /api/v1/projects/{projectId}/sprints/{sprintId}
        // Updates a sprint's name/goal/dates (200 SprintDto, 400 on
        // validation failure, 404 if not found, 409 on a scheduling conflict).
        group.MapPut("/{projectId:guid}/sprints/{sprintId:guid}", UpdateSprintAsync)
            .WithName("UpdateSprint")
            .Produces<SprintDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        // POST /api/v1/projects/{projectId}/sprints/{sprintId}/start
        // Transitions a sprint to Active (200 SprintDto, 404 if not found,
        // 409 if the sprint cannot be started from its current status).
        group.MapPost("/{projectId:guid}/sprints/{sprintId:guid}/start", StartSprintAsync)
            .WithName("StartSprint")
            .Produces<SprintDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        // POST /api/v1/projects/{projectId}/sprints/{sprintId}/complete
        // Transitions a sprint to Completed (200 SprintDto, 404 if not
        // found, 409 if the sprint cannot be completed from its current status).
        group.MapPost("/{projectId:guid}/sprints/{sprintId:guid}/complete", CompleteSprintAsync)
            .WithName("CompleteSprint")
            .Produces<SprintDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        // POST /api/v1/projects/{projectId}/sprints/{sprintId}/cancel
        // Transitions a sprint to Cancelled (200 SprintDto, 404 if not
        // found, 409 if the sprint cannot be cancelled from its current status).
        group.MapPost("/{projectId:guid}/sprints/{sprintId:guid}/cancel", CancelSprintAsync)
            .WithName("CancelSprint")
            .Produces<SprintDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        // DELETE /api/v1/projects/{projectId}/sprints/{sprintId}
        // Deletes a sprint and unassigns (not deletes) its tasks. Callers may
        // delete sprints in their own projects; deleting another user's
        // sprint additionally requires super-admin status (200 bool success
        // flag, 403 if not permitted, 404 if not found).
        group.MapDelete("/{projectId:guid}/sprints/{sprintId:guid}", DeleteSprintAsync)
            .WithName("DeleteSprint")
            .Produces<bool>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    // Handler for POST /api/v1/projects/. Delegates to CreateProjectHandler
    // and returns 201 Created with a Location header on success.
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

    // Handler for GET /api/v1/projects/{projectId}. Delegates to GetProjectByIdHandler.
    private static async Task<IResult> GetProjectAsync(
        Guid projectId,
        GetProjectByIdHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new GetProjectByIdQuery(projectId),
            cancellationToken));

    // Handler for PUT /api/v1/projects/{projectId}. Delegates to UpdateProjectHandler.
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

    // Handler for POST /api/v1/projects/{projectId}/archive. Delegates to ArchiveProjectHandler.
    private static async Task<IResult> ArchiveProjectAsync(
        Guid projectId,
        ArchiveProjectHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new ArchiveProjectCommand(projectId),
            cancellationToken));

    // Handler for DELETE /api/v1/projects/{projectId}. Computes whether the
    // caller is a super-admin and passes that flag to DeleteProjectHandler,
    // which uses it to allow deleting projects the caller does not own.
    private static async Task<IResult> DeleteProjectAsync(
        Guid projectId,
        DeleteProjectHandler handler,
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var isSuperAdmin = await SuperAdminAuthorization.IsSuperAdminAsync(
            currentUser, accounts, configuration, cancellationToken);

        return ApiResult.From(await handler.HandleAsync(
            new DeleteProjectCommand(projectId, isSuperAdmin),
            cancellationToken));
    }

    // Handler for GET /api/v1/projects/{projectId}/board. Delegates to GetProjectBoardHandler.
    private static async Task<IResult> GetBoardAsync(
        Guid projectId,
        GetProjectBoardHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new GetProjectBoardQuery(projectId),
            cancellationToken));

    // Handler for POST /api/v1/projects/{projectId}/categories. Delegates to CreateCategoryHandler.
    private static async Task<IResult> CreateCategoryAsync(
        Guid projectId,
        CreateCategoryRequest request,
        CreateCategoryHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new CreateCategoryCommand(projectId, request.Name),
            cancellationToken));

    // Handler for POST /api/v1/projects/{projectId}/sprints. Delegates to CreateSprintHandler.
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

    // Handler for PUT /api/v1/projects/{projectId}/sprints/{sprintId}. Delegates to UpdateSprintHandler.
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

    // Handler for POST .../sprints/{sprintId}/start. Delegates to StartSprintHandler.
    private static async Task<IResult> StartSprintAsync(
        Guid projectId,
        Guid sprintId,
        StartSprintHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new ChangeSprintStatusCommand(projectId, sprintId),
            cancellationToken));

    // Handler for POST .../sprints/{sprintId}/complete. Delegates to CompleteSprintHandler.
    private static async Task<IResult> CompleteSprintAsync(
        Guid projectId,
        Guid sprintId,
        CompleteSprintHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new ChangeSprintStatusCommand(projectId, sprintId),
            cancellationToken));

    // Handler for POST .../sprints/{sprintId}/cancel. Delegates to CancelSprintHandler.
    private static async Task<IResult> CancelSprintAsync(
        Guid projectId,
        Guid sprintId,
        CancelSprintHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new ChangeSprintStatusCommand(projectId, sprintId),
            cancellationToken));

    // Handler for DELETE .../sprints/{sprintId}. Computes whether the caller
    // is a super-admin and passes that flag to DeleteSprintHandler, which
    // uses it to allow deleting sprints the caller does not own; the sprint's
    // tasks are unassigned rather than deleted.
    private static async Task<IResult> DeleteSprintAsync(
        Guid projectId,
        Guid sprintId,
        DeleteSprintHandler handler,
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var isSuperAdmin = await SuperAdminAuthorization.IsSuperAdminAsync(
            currentUser, accounts, configuration, cancellationToken);

        return ApiResult.From(await handler.HandleAsync(
            new DeleteSprintCommand(projectId, sprintId, isSuperAdmin),
            cancellationToken));
    }
}

/// <summary>Request body for creating a new project category.</summary>
public sealed record CreateCategoryRequest(string Name);
