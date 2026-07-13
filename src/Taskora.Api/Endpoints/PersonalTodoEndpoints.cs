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
                request.Notes),
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
                    request.Notes),
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
}
