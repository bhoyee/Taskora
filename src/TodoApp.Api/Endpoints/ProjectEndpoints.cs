using TodoApp.Api.Contracts;
using TodoApp.Application.Projects;
using TodoApp.Application.Projects.Board;

namespace TodoApp.Api.Endpoints;

internal static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects")
            .WithTags("Projects");

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
        group.MapGet("/{projectId:guid}/board", GetBoardAsync)
            .WithName("GetProjectBoard")
            .Produces<ProjectBoardDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

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

    private static async Task<IResult> GetBoardAsync(
        Guid projectId,
        GetProjectBoardHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new GetProjectBoardQuery(projectId),
            cancellationToken));
}
