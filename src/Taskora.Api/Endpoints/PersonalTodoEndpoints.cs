using TodoApp.Api.Contracts;
using TodoApp.Application.Todos;

namespace TodoApp.Api.Endpoints;

/// <summary>
/// Maps the authenticated personal-todo and daily-routine endpoints under
/// "/api/v1/todos" (per-user scratch todos, distinct from workspace tasks).
/// </summary>
internal static class PersonalTodoEndpoints
{
    /// <summary>
    /// Registers all personal-todo and daily-routine routes. Every route in
    /// this group requires an authenticated user (<c>RequireAuthorization</c>
    /// is applied to the whole group); handlers resolve the current user
    /// internally, so todos/routines are implicitly scoped to their owner.
    /// </summary>
    public static IEndpointRouteBuilder MapPersonalTodoEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/todos")
            .WithTags("Personal Todos")
            .RequireAuthorization();

        // GET /api/v1/todos: paged list of the caller's todos, optionally
        // filtered by date and/or search text. Returns 200 with a page of todos.
        group.MapGet("/", ListTodosAsync)
            .WithName("ListPersonalTodos");
        // GET /api/v1/todos/range: the caller's todos falling within an
        // inclusive [from, to] date range. Returns 200 with the matching todos.
        group.MapGet("/range", ListTodosForRangeAsync)
            .WithName("ListPersonalTodosForRange");
        // GET /api/v1/todos/routines: paged list of the caller's recurring
        // daily routines. Returns 200 with a page of routines.
        group.MapGet("/routines", ListDailyRoutinesAsync)
            .WithName("ListDailyRoutines");
        // POST /api/v1/todos/routines: creates a new daily routine for the
        // caller. Returns 201 with the created routine, or 400 on validation failure.
        group.MapPost("/routines", CreateDailyRoutineAsync)
            .WithName("CreateDailyRoutine")
            .Produces<DailyRoutineDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        // PUT /api/v1/todos/routines/{routineId}: updates an existing daily
        // routine owned by the caller.
        group.MapPut("/routines/{routineId:guid}", UpdateDailyRoutineAsync)
            .WithName("UpdateDailyRoutine");
        // DELETE /api/v1/todos/routines/{routineId}: removes a daily routine
        // owned by the caller.
        group.MapDelete("/routines/{routineId:guid}", DeleteDailyRoutineAsync)
            .WithName("DeleteDailyRoutine");
        // POST /api/v1/todos: creates a new personal todo for the caller.
        // Returns 201 with the created todo, or 400 on validation failure.
        group.MapPost("/", CreateTodoAsync)
            .WithName("CreatePersonalTodo")
            .Produces<PersonalTodoDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        // PUT /api/v1/todos/{todoId}: updates an existing todo owned by the caller.
        group.MapPut("/{todoId:guid}", UpdateTodoAsync)
            .WithName("UpdatePersonalTodo");
        // POST /api/v1/todos/{todoId}/complete: marks the todo as completed.
        group.MapPost("/{todoId:guid}/complete", CompleteTodoAsync)
            .WithName("CompletePersonalTodo");
        // POST /api/v1/todos/{todoId}/reopen: reverts a completed todo back to open.
        group.MapPost("/{todoId:guid}/reopen", ReopenTodoAsync)
            .WithName("ReopenPersonalTodo");
        // DELETE /api/v1/todos/{todoId}: permanently removes a todo owned by the caller.
        group.MapDelete("/{todoId:guid}", DeleteTodoAsync)
            .WithName("DeletePersonalTodo");
        // POST /api/v1/todos/{todoId}/comments: appends a comment to the todo.
        // Returns 200 with the updated todo, 400 on invalid input, or 404 if
        // the todo doesn't exist (or isn't owned by the caller).
        group.MapPost("/{todoId:guid}/comments", AddTodoCommentAsync)
            .WithName("AddPersonalTodoComment")
            .Produces<PersonalTodoDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    // Handles GET /: builds a ListPersonalTodosQuery from optional filters,
    // defaulting page number/size to 1/10 when omitted or zero.
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

    // Handles GET /range: fetches todos between the given dates (inclusive).
    private static async Task<IResult> ListTodosForRangeAsync(
        DateOnly from,
        DateOnly to,
        ListPersonalTodosForRangeHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new ListPersonalTodosForRangeQuery(from, to),
                cancellationToken));

    // Handles POST /: creates the todo, defaulting the date to today and the
    // priority to Medium when not supplied, and returns 201 with a Location
    // header on success.
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

    // Handles PUT /{todoId}: applies the same date/priority defaulting as
    // create when fields are omitted.
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

    // Handles POST /{todoId}/complete.
    private static async Task<IResult> CompleteTodoAsync(
        Guid todoId,
        CompletePersonalTodoHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new CompletePersonalTodoCommand(todoId),
                cancellationToken));

    // Handles POST /{todoId}/reopen.
    private static async Task<IResult> ReopenTodoAsync(
        Guid todoId,
        ReopenPersonalTodoHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new ReopenPersonalTodoCommand(todoId),
                cancellationToken));

    // Handles DELETE /{todoId}.
    private static async Task<IResult> DeleteTodoAsync(
        Guid todoId,
        DeletePersonalTodoHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new DeletePersonalTodoCommand(todoId),
                cancellationToken));

    // Handles POST /{todoId}/comments.
    private static async Task<IResult> AddTodoCommentAsync(
        Guid todoId,
        AddPersonalTodoCommentRequest request,
        AddPersonalTodoCommentHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new AddPersonalTodoCommentCommand(todoId, request.Body),
                cancellationToken));

    // Handles GET /routines: paged list, defaulting page number/size to 1/10.
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

    // Handles POST /routines: creates the routine, defaulting priority to
    // High and start date to today when not supplied.
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

    // Handles PUT /routines/{routineId}, including toggling IsActive.
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

    // Handles DELETE /routines/{routineId}.
    private static async Task<IResult> DeleteDailyRoutineAsync(
        Guid routineId,
        DeleteDailyRoutineHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(
            await handler.HandleAsync(
                new DeleteDailyRoutineCommand(routineId),
                cancellationToken));
}
