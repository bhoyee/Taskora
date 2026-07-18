using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;
using TodoApp.Domain.Todos;
using static TodoApp.Application.Todos.PersonalTodoHandlerHelpers;

namespace TodoApp.Application.Todos;

public sealed class ListPersonalTodosHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IClock clock,
    IBusinessDateProvider dates,
    ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<PersonalTodoDto>>> HandleAsync(
        ListPersonalTodosQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = RequireAuthenticatedUser(currentUser);
        if (!authorization.IsSuccess)
        {
            return Result<PagedResult<PersonalTodoDto>>.Failure(
                authorization.Error);
        }

        if (query.PageNumber < 1)
        {
            return Validation<PagedResult<PersonalTodoDto>>(
                "Page number must be at least 1.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            return Validation<PagedResult<PersonalTodoDto>>(
                "Page size must be between 1 and 100.");
        }

        var today = dates.Today;
        var targetDate = query.Date ?? today;
        if (targetDate == today)
        {
            var carried = await todos.ListIncompleteBeforeAsync(
                currentUser.UserId,
                today,
                cancellationToken);
            foreach (var todo in carried)
            {
                todo.CarryOverTo(today, clock.UtcNow);
            }

            if (carried.Count > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        var searchResult = await todos.SearchAsync(
            new PersonalTodoSearchCriteria(
                currentUser.UserId,
                targetDate,
                string.IsNullOrWhiteSpace(query.Search)
                    ? null
                    : query.Search.Trim(),
                query.PageNumber,
                query.PageSize),
            cancellationToken);

        return Result<PagedResult<PersonalTodoDto>>.Success(
            new PagedResult<PersonalTodoDto>(
                searchResult.Items.Select(ToDto).ToArray(),
                searchResult.TotalCount,
                query.PageNumber,
                query.PageSize));
    }
}

public sealed class CreatePersonalTodoHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<PersonalTodoDto>> HandleAsync(
        CreatePersonalTodoCommand command,
        CancellationToken cancellationToken)
    {
        var authorization = RequireAuthenticatedUser(currentUser);
        if (!authorization.IsSuccess)
        {
            return Result<PersonalTodoDto>.Failure(authorization.Error);
        }

        try
        {
            var todo = PersonalTodo.Create(
                identifiers.NewId(),
                currentUser.UserId,
                command.Title,
                command.TodoDate,
                command.Notes,
                clock.UtcNow);

            await todos.AddAsync(todo, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<PersonalTodoDto>.Success(ToDto(todo));
        }
        catch (DomainValidationException exception)
        {
            return Validation<PersonalTodoDto>(exception.Message);
        }
    }
}

public sealed class UpdatePersonalTodoHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<PersonalTodoDto>> HandleAsync(
        UpdatePersonalTodoCommand command,
        CancellationToken cancellationToken)
    {
        var todo = await GetOwnedTodoAsync(
            todos,
            currentUser,
            command.TodoId,
            cancellationToken);
        if (!todo.IsSuccess)
        {
            return Result<PersonalTodoDto>.Failure(todo.Error);
        }

        try
        {
            todo.Value.Update(
                command.Title,
                command.TodoDate,
                command.Notes,
                clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<PersonalTodoDto>.Success(ToDto(todo.Value));
        }
        catch (DomainValidationException exception)
        {
            return Validation<PersonalTodoDto>(exception.Message);
        }
    }
}

public sealed class CompletePersonalTodoHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    public Task<Result<PersonalTodoDto>> HandleAsync(
        CompletePersonalTodoCommand command,
        CancellationToken cancellationToken) =>
        MutateTodoAsync(
            todos,
            unitOfWork,
            currentUser,
            command.TodoId,
            todo => todo.Complete(clock.UtcNow),
            cancellationToken);
}

public sealed class ReopenPersonalTodoHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    public Task<Result<PersonalTodoDto>> HandleAsync(
        ReopenPersonalTodoCommand command,
        CancellationToken cancellationToken) =>
        MutateTodoAsync(
            todos,
            unitOfWork,
            currentUser,
            command.TodoId,
            todo => todo.Reopen(clock.UtcNow),
            cancellationToken);
}

public sealed class DeletePersonalTodoHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(
        DeletePersonalTodoCommand command,
        CancellationToken cancellationToken)
    {
        var todo = await GetOwnedTodoAsync(
            todos,
            currentUser,
            command.TodoId,
            cancellationToken);
        if (!todo.IsSuccess)
        {
            return Result<bool>.Failure(todo.Error);
        }

        await todos.RemoveAsync(todo.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}

internal static class PersonalTodoMapping
{
    public static PersonalTodoDto ToDto(PersonalTodo todo) =>
        new(
            todo.Id,
            todo.Title,
            todo.TodoDate,
            todo.OriginalTodoDate,
            todo.CarriedOverFromDate,
            todo.Notes,
            todo.IsCompleted,
            todo.CreatedAt,
            todo.UpdatedAt,
            todo.CompletedAt);
}

file static class PersonalTodoHandlerHelpers
{
    public static PersonalTodoDto ToDto(PersonalTodo todo) =>
        PersonalTodoMapping.ToDto(todo);

    public static Result<bool> RequireAuthenticatedUser(
        ICurrentUser currentUser) =>
        currentUser.IsAuthenticated
            ? Result<bool>.Success(true)
            : Result<bool>.Failure(new ApplicationError(
                "todo.auth_required",
                "Sign in before managing personal todos.",
                ErrorType.Unauthorized));

    public static async Task<Result<PersonalTodo>> GetOwnedTodoAsync(
        IPersonalTodoRepository todos,
        ICurrentUser currentUser,
        Guid todoId,
        CancellationToken cancellationToken)
    {
        var authorization = RequireAuthenticatedUser(currentUser);
        if (!authorization.IsSuccess)
        {
            return Result<PersonalTodo>.Failure(authorization.Error);
        }

        var todo = await todos.GetByIdAsync(todoId, cancellationToken);
        if (todo is null || todo.UserId != currentUser.UserId)
        {
            return Result<PersonalTodo>.Failure(new ApplicationError(
                "todo.not_found",
                "The todo was not found.",
                ErrorType.NotFound));
        }

        return Result<PersonalTodo>.Success(todo);
    }

    public static async Task<Result<PersonalTodoDto>> MutateTodoAsync(
        IPersonalTodoRepository todos,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        Guid todoId,
        Action<PersonalTodo> mutate,
        CancellationToken cancellationToken)
    {
        var todo = await GetOwnedTodoAsync(
            todos,
            currentUser,
            todoId,
            cancellationToken);
        if (!todo.IsSuccess)
        {
            return Result<PersonalTodoDto>.Failure(todo.Error);
        }

        mutate(todo.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PersonalTodoDto>.Success(ToDto(todo.Value));
    }

    public static Result<T> Validation<T>(string message) =>
        Result<T>.Failure(new ApplicationError(
            "todo.validation",
            message,
            ErrorType.Validation));
}
