using TodoApp.Api.Contracts;
using TodoApp.Application.Common;
using TodoApp.Application.Tasks.CreateTask;
using TodoApp.Application.Tasks.Activity;
using TodoApp.Application.Tasks.Assignment;
using TodoApp.Application.Tasks.Lifecycle;
using TodoApp.Application.Tasks.Maintenance;
using TodoApp.Application.Tasks.Metadata;
using TodoApp.Application.Tasks.Queries;
using TodoApp.Domain.Tasks;

namespace TodoApp.Api.Endpoints;

internal static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/v1/projects/{projectId:guid}/tasks",
                CreateTaskAsync)
            .WithTags("Tasks")
            .RequireAuthorization()
            .WithName("CreateTask")
            .Produces<TaskDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var group = endpoints.MapGroup("/api/v1/tasks")
            .WithTags("Tasks")
            .RequireAuthorization();

        group.MapGet("/", SearchTasksAsync)
            .WithName("SearchTasks");
        group.MapGet("/{taskId:guid}", GetTaskAsync)
            .WithName("GetTask")
            .Produces<TaskDetailsDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/{taskId:guid}/activity", GetActivityAsync)
            .WithName("GetTaskActivity")
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPut("/{taskId:guid}", UpdateTaskAsync)
            .WithName("UpdateTask");
        group.MapPut("/{taskId:guid}/planning", UpdatePlanningAsync)
            .WithName("UpdateTaskPlanning");
        group.MapPost("/{taskId:guid}/ready", MoveToReadyAsync)
            .WithName("MoveTaskToReady");
        group.MapPost("/{taskId:guid}/start", StartTaskAsync)
            .WithName("StartTask");
        group.MapPost("/{taskId:guid}/complete", CompleteTaskAsync)
            .WithName("CompleteTask");
        group.MapPost("/{taskId:guid}/reopen", ReopenTaskAsync)
            .WithName("ReopenTask");
        group.MapPost("/{taskId:guid}/block", BlockTaskAsync)
            .WithName("BlockTask");
        group.MapPost("/{taskId:guid}/unblock", UnblockTaskAsync)
            .WithName("UnblockTask");
        group.MapPost("/{taskId:guid}/dependencies", AddDependencyAsync)
            .WithName("AddTaskDependency");
        group.MapDelete(
                "/{taskId:guid}/dependencies/{dependencyId:guid}",
                RemoveDependencyAsync)
            .WithName("RemoveTaskDependency");
        group.MapPut("/{taskId:guid}/assignment", AssignTaskAsync)
            .WithName("AssignTask")
            .RequireAuthorization();
        group.MapDelete("/{taskId:guid}/assignment", UnassignTaskAsync)
            .WithName("UnassignTask")
            .RequireAuthorization();
        group.MapPut("/{taskId:guid}/category", UpdateCategoryAsync)
            .WithName("UpdateTaskCategory");
        group.MapPost("/{taskId:guid}/tags", AddTagAsync)
            .WithName("AddTaskTag");
        group.MapDelete("/{taskId:guid}/tags/{tag}", RemoveTagAsync)
            .WithName("RemoveTaskTag");
        group.MapPost("/{taskId:guid}/notes", AddNoteAsync)
            .WithName("AddTaskNote");

        return endpoints;
    }

    private static async Task<IResult> CreateTaskAsync(
        Guid projectId,
        CreateTaskRequest request,
        CreateTaskHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateTaskCommand(
                projectId,
                request.Title,
                request.DueDate,
                request.Effort,
                request.BusinessValue,
                request.Urgency,
                request.RiskReduction),
            cancellationToken);

        return result.IsSuccess
            ? Results.Created(
                $"/api/v1/tasks/{result.Value.Id}",
                result.Value)
            : ApiResult.From(result);
    }

    private static async Task<IResult> GetTaskAsync(
        Guid taskId,
        GetTaskByIdHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new GetTaskByIdQuery(taskId),
            cancellationToken));

    private static async Task<IResult> GetActivityAsync(
        Guid taskId,
        GetTaskActivityHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new GetTaskActivityQuery(taskId),
            cancellationToken));

    private static async Task<IResult> SearchTasksAsync(
        Guid? projectId,
        Guid? workspaceId,
        TaskItemStatus? status,
        bool? isBlocked,
        Guid? categoryId,
        string? tag,
        string? search,
        TaskSortBy? sortBy,
        int pageNumber,
        int pageSize,
        SearchTasksHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new SearchTasksQuery(
                projectId,
                workspaceId,
                status,
                isBlocked,
                categoryId,
                tag,
                search,
                sortBy ?? TaskSortBy.CreatedDescending,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken));

    private static async Task<IResult> UpdateTaskAsync(
        Guid taskId,
        UpdateTaskRequest request,
        UpdateTaskHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new UpdateTaskCommand(
                taskId,
                request.Title,
                request.DueDate,
                request.Effort),
            cancellationToken));

    private static async Task<IResult> UpdatePlanningAsync(
        Guid taskId,
        UpdatePlanningFactorsRequest request,
        UpdatePlanningFactorsHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new UpdatePlanningFactorsCommand(
                taskId,
                request.BusinessValue,
                request.Urgency,
                request.RiskReduction,
                request.Effort),
            cancellationToken));

    private static Task<IResult> MoveToReadyAsync(
        Guid taskId,
        MoveTaskToReadyHandler handler,
        CancellationToken cancellationToken) =>
        HandleStatusAsync(
            handler.HandleAsync(
                new MoveTaskToReadyCommand(taskId),
                cancellationToken));

    private static Task<IResult> StartTaskAsync(
        Guid taskId,
        StartTaskHandler handler,
        CancellationToken cancellationToken) =>
        HandleStatusAsync(
            handler.HandleAsync(
                new StartTaskCommand(taskId),
                cancellationToken));

    private static Task<IResult> CompleteTaskAsync(
        Guid taskId,
        CompleteTaskHandler handler,
        CancellationToken cancellationToken) =>
        HandleStatusAsync(
            handler.HandleAsync(
                new CompleteTaskCommand(taskId),
                cancellationToken));

    private static Task<IResult> ReopenTaskAsync(
        Guid taskId,
        ReopenTaskHandler handler,
        CancellationToken cancellationToken) =>
        HandleStatusAsync(
            handler.HandleAsync(
                new ReopenTaskCommand(taskId),
                cancellationToken));

    private static Task<IResult> BlockTaskAsync(
        Guid taskId,
        BlockTaskRequest request,
        BlockTaskHandler handler,
        CancellationToken cancellationToken) =>
        HandleStatusAsync(
            handler.HandleAsync(
                new BlockTaskCommand(taskId, request.Reason),
                cancellationToken));

    private static Task<IResult> UnblockTaskAsync(
        Guid taskId,
        UnblockTaskHandler handler,
        CancellationToken cancellationToken) =>
        HandleStatusAsync(
            handler.HandleAsync(
                new UnblockTaskCommand(taskId),
                cancellationToken));

    private static async Task<IResult> AddDependencyAsync(
        Guid taskId,
        AddDependencyRequest request,
        AddTaskDependencyHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new AddTaskDependencyCommand(taskId, request.DependencyId),
            cancellationToken));

    private static Task<IResult> RemoveDependencyAsync(
        Guid taskId,
        Guid dependencyId,
        RemoveTaskDependencyHandler handler,
        CancellationToken cancellationToken) =>
        HandleStatusAsync(
            handler.HandleAsync(
                new RemoveTaskDependencyCommand(taskId, dependencyId),
                cancellationToken));

    private static async Task<IResult> AssignTaskAsync(
        Guid taskId,
        AssignTaskRequest request,
        AssignTaskHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new AssignTaskCommand(taskId, request.UserId),
            cancellationToken));

    private static async Task<IResult> UnassignTaskAsync(
        Guid taskId,
        UnassignTaskHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new UnassignTaskCommand(taskId),
            cancellationToken));

    private static async Task<IResult> UpdateCategoryAsync(
        Guid taskId,
        UpdateTaskCategoryRequest request,
        UpdateTaskCategoryHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new UpdateTaskCategoryCommand(taskId, request.CategoryId),
            cancellationToken));

    private static async Task<IResult> AddTagAsync(
        Guid taskId,
        AddTaskTagRequest request,
        AddTaskTagHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new AddTaskTagCommand(taskId, request.Name ?? request.Tag ?? string.Empty),
            cancellationToken));

    private static async Task<IResult> RemoveTagAsync(
        Guid taskId,
        string tag,
        RemoveTaskTagHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new RemoveTaskTagCommand(taskId, tag),
            cancellationToken));

    private static async Task<IResult> AddNoteAsync(
        Guid taskId,
        AddTaskNoteRequest request,
        AddTaskNoteHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new AddTaskNoteCommand(taskId, request.Body),
            cancellationToken));

    private static async Task<IResult> HandleStatusAsync(
        Task<Result<TaskItemStatus>> operation) =>
        ApiResult.From(await operation);
}

public sealed record UpdateTaskCategoryRequest(Guid? CategoryId);

public sealed record AddTaskTagRequest(string? Name = null, string? Tag = null);

public sealed record AddTaskNoteRequest(string Body);
