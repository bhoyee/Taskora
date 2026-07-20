using TodoApp.Api.Contracts;
using TodoApp.Application.Todos;

namespace TodoApp.Api.Endpoints;

internal static class PersonalTodoEndpoints
{
    public static IEndpointRouteBuilder MapPersonalTodoEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/todos")
            .WithTags("Personal Todos")
            .RequireAuthorization();

        group.MapGet("/", ListTodosAsync)
            .WithName("ListPersonalTodos");
        group.MapGet("/routines", ListDailyRoutinesAsync)
            .WithName("ListDailyRoutines");
        group.MapPost("/routines", CreateDailyRoutineAsync)
            .WithName("CreateDailyRoutine")
            .Produces<DailyRoutineDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapPut("/routines/{routineId:guid}", UpdateDailyRoutineAsync)
            .WithName("UpdateDailyRoutine");
        group.MapDelete("/routines/{routineId:guid}", DeleteDailyRoutineAsync)
            .WithName("DeleteDailyRoutine");
        group.MapPost("/", CreateTodoAsync)
            .WithName("CreatePersonalTodo")
            .Produces<PersonalTodoDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapPut("/{todoId:guid}", UpdateTodoAsync)
            .WithName("UpdatePersonalTodo");
        group.MapPost("/{todoId:guid}/complete", CompleteTodoAsync)
            .WithName("CompletePersonalTodo");
        group.MapPost("/{todoId:guid}/reopen", ReopenTodoAsync)
            .WithName("ReopenPersonalTodo");
        group.MapDelete("/{todoId:guid}", DeleteTodoAsync)
            .WithName("DeletePersonalTodo");

        return endpoints;
    }

    private static async Task<IResult> ListTodosAsync(
        DateOnly? date,
        string? search,
        int? pageNumber,
        int? pageSize,
        ListPersonalTodosHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new ListPersonalTodosQuery(
                    date,
                    search,
                    pageNumber is null or 0 ? 1 : pageNumber.Value,
                    pageSize is null or 0 ? 10 : pageSize.Value),
                cancellationToken));

    private static async Task<IResult> CreateTodoAsync(
        CreatePersonalTodoRequest request,
        CreatePersonalTodoHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreatePersonalTodoCommand(
                request.Title,
                request.TodoDate ?? DateOnly.FromDateTime(DateTime.Today),
                request.Notes,
                request.Priority ?? TodoApp.Domain.Todos.TodoPriority.Medium),
            cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/todos/{result.Value.Id}", result.Value)
            : ApiResult.From(result);
    }

    private static async Task<IResult> UpdateTodoAsync(
        Guid todoId,
        UpdatePersonalTodoRequest request,
        UpdatePersonalTodoHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new UpdatePersonalTodoCommand(
                    todoId,
                    request.Title,
                    request.TodoDate ?? DateOnly.FromDateTime(DateTime.Today),
                    request.Notes,
                    request.Priority ?? TodoApp.Domain.Todos.TodoPriority.Medium),
                cancellationToken));

    private static async Task<IResult> CompleteTodoAsync(
        Guid todoId,
        CompletePersonalTodoHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new CompletePersonalTodoCommand(todoId),
                cancellationToken));

    private static async Task<IResult> ReopenTodoAsync(
        Guid todoId,
        ReopenPersonalTodoHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new ReopenPersonalTodoCommand(todoId),
                cancellationToken));

    private static async Task<IResult> DeleteTodoAsync(
        Guid todoId,
        DeletePersonalTodoHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new DeletePersonalTodoCommand(todoId),
                cancellationToken));

    private static async Task<IResult> ListDailyRoutinesAsync(
        int? pageNumber,
        int? pageSize,
        ListDailyRoutinesHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new ListDailyRoutinesQuery(
                    pageNumber is null or 0 ? 1 : pageNumber.Value,
                    pageSize is null or 0 ? 10 : pageSize.Value),
                cancellationToken));

    private static async Task<IResult> CreateDailyRoutineAsync(
        CreateDailyRoutineRequest request,
        CreateDailyRoutineHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateDailyRoutineCommand(
                request.Title,
                request.Notes,
                request.Priority ?? TodoApp.Domain.Todos.TodoPriority.High,
                request.StartDate ?? DateOnly.FromDateTime(DateTime.Today),
                request.EndDate),
            cancellationToken);

        return result.IsSuccess
            ? Results.Created(
                $"/api/v1/todos/routines/{result.Value.Id}",
                result.Value)
            : ApiResult.From(result);
    }

    private static async Task<IResult> UpdateDailyRoutineAsync(
        Guid routineId,
        UpdateDailyRoutineRequest request,
        UpdateDailyRoutineHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new UpdateDailyRoutineCommand(
                    routineId,
                    request.Title,
                    request.Notes,
                    request.Priority ?? TodoApp.Domain.Todos.TodoPriority.High,
                    request.StartDate ?? DateOnly.FromDateTime(DateTime.Today),
                    request.EndDate,
                    request.IsActive),
                cancellationToken));

    private static async Task<IResult> DeleteDailyRoutineAsync(
        Guid routineId,
        DeleteDailyRoutineHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new DeleteDailyRoutineCommand(routineId),
                cancellationToken));
}
