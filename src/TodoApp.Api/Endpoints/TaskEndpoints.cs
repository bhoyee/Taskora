using TodoApp.Api.Contracts;
using TodoApp.Api.Realtime;
using TodoApp.Application.Abstractions;
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
        group.MapDelete("/{taskId:guid}", DeleteTaskAsync)
            .WithName("DeleteTask");
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
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
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

        if (!result.IsSuccess)
        {
            return ApiResult.From(result);
        }

        await PublishProjectChangeAsync(
            projectId,
            "task.created",
            result.Value.Id,
            projects,
            events,
            currentUser,
            cancellationToken);
        return Results.Created(
            $"/api/v1/tasks/{result.Value.Id}",
            result.Value);
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
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new UpdateTaskCommand(
                taskId,
                request.Title,
                request.DueDate,
                request.Effort),
            cancellationToken);
        await PublishTaskChangeAsync(
            result.IsSuccess,
            taskId,
            "task.updated",
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);
        return ApiResult.From(result);
    }

    private static async Task<IResult> UpdatePlanningAsync(
        Guid taskId,
        UpdatePlanningFactorsRequest request,
        UpdatePlanningFactorsHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new UpdatePlanningFactorsCommand(
                taskId,
                request.BusinessValue,
                request.Urgency,
                request.RiskReduction,
                request.Effort),
            cancellationToken);
        await PublishTaskChangeAsync(
            result.IsSuccess,
            taskId,
            "task.planning-updated",
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);
        return ApiResult.From(result);
    }

    private static async Task<IResult> MoveToReadyAsync(
        Guid taskId,
        MoveTaskToReadyHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await HandleStatusAsync(
            taskId,
            "task.ready",
            handler.HandleAsync(
                new MoveTaskToReadyCommand(taskId),
                cancellationToken),
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);

    private static async Task<IResult> StartTaskAsync(
        Guid taskId,
        StartTaskHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await HandleStatusAsync(
            taskId,
            "task.started",
            handler.HandleAsync(
                new StartTaskCommand(taskId),
                cancellationToken),
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);

    private static async Task<IResult> CompleteTaskAsync(
        Guid taskId,
        CompleteTaskHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await HandleStatusAsync(
            taskId,
            "task.completed",
            handler.HandleAsync(
                new CompleteTaskCommand(taskId),
                cancellationToken),
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);

    private static async Task<IResult> ReopenTaskAsync(
        Guid taskId,
        ReopenTaskHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await HandleStatusAsync(
            taskId,
            "task.reopened",
            handler.HandleAsync(
                new ReopenTaskCommand(taskId),
                cancellationToken),
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);

    private static async Task<IResult> DeleteTaskAsync(
        Guid taskId,
        DeleteTaskHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        var result = await handler.HandleAsync(
            new DeleteTaskCommand(taskId),
            cancellationToken);
        if (result.IsSuccess && task is not null)
        {
            await PublishProjectChangeAsync(
                task.ProjectId,
                "task.deleted",
                taskId,
                projects,
                events,
                currentUser,
                cancellationToken);
        }

        return ApiResult.From(result);
    }

    private static async Task<IResult> BlockTaskAsync(
        Guid taskId,
        BlockTaskRequest request,
        BlockTaskHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await HandleStatusAsync(
            taskId,
            "task.blocked",
            handler.HandleAsync(
                new BlockTaskCommand(taskId, request.Reason),
                cancellationToken),
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);

    private static async Task<IResult> UnblockTaskAsync(
        Guid taskId,
        UnblockTaskHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await HandleStatusAsync(
            taskId,
            "task.unblocked",
            handler.HandleAsync(
                new UnblockTaskCommand(taskId),
                cancellationToken),
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);

    private static async Task<IResult> AddDependencyAsync(
        Guid taskId,
        AddDependencyRequest request,
        AddTaskDependencyHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AddTaskDependencyCommand(taskId, request.DependencyId),
            cancellationToken);
        await PublishTaskChangeAsync(
            result.IsSuccess,
            taskId,
            "task.dependency-added",
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);
        return ApiResult.From(result);
    }

    private static async Task<IResult> RemoveDependencyAsync(
        Guid taskId,
        Guid dependencyId,
        RemoveTaskDependencyHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await HandleStatusAsync(
            taskId,
            "task.dependency-removed",
            handler.HandleAsync(
                new RemoveTaskDependencyCommand(taskId, dependencyId),
                cancellationToken),
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);

    private static async Task<IResult> AssignTaskAsync(
        Guid taskId,
        AssignTaskRequest request,
        AssignTaskHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AssignTaskCommand(taskId, request.UserId),
            cancellationToken);
        await PublishTaskChangeAsync(
            result.IsSuccess,
            taskId,
            "task.assigned",
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);
        return ApiResult.From(result);
    }

    private static async Task<IResult> UnassignTaskAsync(
        Guid taskId,
        UnassignTaskHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new UnassignTaskCommand(taskId),
            cancellationToken);
        await PublishTaskChangeAsync(
            result.IsSuccess,
            taskId,
            "task.unassigned",
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);
        return ApiResult.From(result);
    }

    private static async Task<IResult> UpdateCategoryAsync(
        Guid taskId,
        UpdateTaskCategoryRequest request,
        UpdateTaskCategoryHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new UpdateTaskCategoryCommand(taskId, request.CategoryId),
            cancellationToken);
        await PublishTaskChangeAsync(
            result.IsSuccess,
            taskId,
            "task.category-updated",
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);
        return ApiResult.From(result);
    }

    private static async Task<IResult> AddTagAsync(
        Guid taskId,
        AddTaskTagRequest request,
        AddTaskTagHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AddTaskTagCommand(taskId, request.Name ?? request.Tag ?? string.Empty),
            cancellationToken);
        await PublishTaskChangeAsync(
            result.IsSuccess,
            taskId,
            "task.tag-added",
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);
        return ApiResult.From(result);
    }

    private static async Task<IResult> RemoveTagAsync(
        Guid taskId,
        string tag,
        RemoveTaskTagHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RemoveTaskTagCommand(taskId, tag),
            cancellationToken);
        await PublishTaskChangeAsync(
            result.IsSuccess,
            taskId,
            "task.tag-removed",
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);
        return ApiResult.From(result);
    }

    private static async Task<IResult> AddNoteAsync(
        Guid taskId,
        AddTaskNoteRequest request,
        AddTaskNoteHandler handler,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AddTaskNoteCommand(taskId, request.Body),
            cancellationToken);
        await PublishTaskChangeAsync(
            result.IsSuccess,
            taskId,
            "task.note-added",
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);
        return ApiResult.From(result);
    }

    private static async Task<IResult> HandleStatusAsync(
        Guid taskId,
        string eventType,
        Task<Result<TaskItemStatus>> operation,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await operation;
        await PublishTaskChangeAsync(
            result.IsSuccess,
            taskId,
            eventType,
            tasks,
            projects,
            events,
            currentUser,
            cancellationToken);
        return ApiResult.From(result);
    }

    private static async Task PublishTaskChangeAsync(
        bool succeeded,
        Guid taskId,
        string eventType,
        ITaskRepository tasks,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!succeeded)
        {
            return;
        }

        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null)
        {
            return;
        }

        await PublishProjectChangeAsync(
            task.ProjectId,
            eventType,
            taskId,
            projects,
            events,
            currentUser,
            cancellationToken);
    }

    private static async Task PublishProjectChangeAsync(
        Guid projectId,
        string eventType,
        Guid? entityId,
        IProjectRepository projects,
        WorkspaceEventBroadcaster events,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null || project.WorkspaceId == Guid.Empty)
        {
            return;
        }

        await events.PublishAsync(
            project.WorkspaceId,
            eventType,
            "Task",
            entityId,
            currentUser.UserId,
            cancellationToken);
    }
}

public sealed record UpdateTaskCategoryRequest(Guid? CategoryId);

public sealed record AddTaskTagRequest(string? Name = null, string? Tag = null);

public sealed record AddTaskNoteRequest(string Body);
