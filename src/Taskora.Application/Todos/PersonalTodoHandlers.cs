using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Notifications;
using TodoApp.Domain.Common;
using TodoApp.Domain.Todos;
using static TodoApp.Application.Todos.PersonalTodoHandlerHelpers;

namespace TodoApp.Application.Todos;

public sealed class ListPersonalTodosHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IClock clock,
    IBusinessDateProvider dates,
    INotificationEmailSender emailSender,
    GenerateDailyRoutineTodosHandler dailyRoutines,
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
            await dailyRoutines.HandleAsync(
                new GenerateDailyRoutineTodosCommand(today),
                cancellationToken);
            var carried = await todos.ListIncompleteBeforeAsync(
                currentUser.UserId,
                today,
                cancellationToken);
            var carryOverItems = carried
                .Select(todo => new PersonalTodoCarryOverEmailItem(
                    todo.Title,
                    todo.TodoDate))
                .ToArray();
            foreach (var todo in carried)
            {
                todo.CarryOverTo(today, clock.UtcNow);
            }

            if (carried.Count > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await SendCarryOverEmailAsync(
                    carryOverItems,
                    today,
                    cancellationToken);
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

    private async Task SendCarryOverEmailAsync(
        IReadOnlyCollection<PersonalTodoCarryOverEmailItem> items,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var owners = await todos.ListOwnersAsync(
            [currentUser.UserId],
            cancellationToken);
        var owner = owners.FirstOrDefault();
        if (owner is null || string.IsNullOrWhiteSpace(owner.Email))
        {
            return;
        }

        await emailSender.SendAsync(
            PersonalTodoCarryOverEmailFactory.Build(
                owner,
                items,
                today),
            cancellationToken);
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
                command.Priority,
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
                command.Priority,
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
            todo.Priority,
            todo.DailyRoutineId,
            todo.IsGeneratedFromDailyRoutine,
            todo.IsCompleted,
            todo.CreatedAt,
            todo.UpdatedAt,
            todo.CompletedAt);
}

public sealed class ListDailyRoutinesHandler(
    IDailyRoutineRepository routines,
    ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<DailyRoutineDto>>> HandleAsync(
        ListDailyRoutinesQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = RequireAuthenticatedUser(currentUser);
        if (!authorization.IsSuccess)
        {
            return Result<PagedResult<DailyRoutineDto>>.Failure(
                authorization.Error);
        }

        if (query.PageNumber < 1)
        {
            return Validation<PagedResult<DailyRoutineDto>>(
                "Page number must be at least 1.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            return Validation<PagedResult<DailyRoutineDto>>(
                "Page size must be between 1 and 100.");
        }

        var result = await routines.SearchAsync(
            currentUser.UserId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResult<DailyRoutineDto>>.Success(
            new PagedResult<DailyRoutineDto>(
                result.Items.Select(ToDto).ToArray(),
                result.TotalCount,
                query.PageNumber,
                query.PageSize));
    }
}

public sealed class CreateDailyRoutineHandler(
    IDailyRoutineRepository routines,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<DailyRoutineDto>> HandleAsync(
        CreateDailyRoutineCommand command,
        CancellationToken cancellationToken)
    {
        var authorization = RequireAuthenticatedUser(currentUser);
        if (!authorization.IsSuccess)
        {
            return Result<DailyRoutineDto>.Failure(authorization.Error);
        }

        try
        {
            var routine = DailyRoutine.Create(
                identifiers.NewId(),
                currentUser.UserId,
                command.Title,
                command.Notes,
                command.Priority,
                command.StartDate,
                command.EndDate,
                clock.UtcNow);
            await routines.AddAsync(routine, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<DailyRoutineDto>.Success(ToDto(routine));
        }
        catch (DomainValidationException exception)
        {
            return Validation<DailyRoutineDto>(exception.Message);
        }
    }
}

public sealed class UpdateDailyRoutineHandler(
    IDailyRoutineRepository routines,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<DailyRoutineDto>> HandleAsync(
        UpdateDailyRoutineCommand command,
        CancellationToken cancellationToken)
    {
        var routine = await GetOwnedRoutineAsync(
            routines,
            currentUser,
            command.RoutineId,
            cancellationToken);
        if (!routine.IsSuccess)
        {
            return Result<DailyRoutineDto>.Failure(routine.Error);
        }

        try
        {
            routine.Value.Update(
                command.Title,
                command.Notes,
                command.Priority,
                command.StartDate,
                command.EndDate,
                command.IsActive,
                clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<DailyRoutineDto>.Success(ToDto(routine.Value));
        }
        catch (DomainValidationException exception)
        {
            return Validation<DailyRoutineDto>(exception.Message);
        }
    }
}

public sealed class DeleteDailyRoutineHandler(
    IDailyRoutineRepository routines,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(
        DeleteDailyRoutineCommand command,
        CancellationToken cancellationToken)
    {
        var routine = await GetOwnedRoutineAsync(
            routines,
            currentUser,
            command.RoutineId,
            cancellationToken);
        if (!routine.IsSuccess)
        {
            return Result<bool>.Failure(routine.Error);
        }

        await routines.RemoveAsync(routine.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}

public sealed class GenerateDailyRoutineTodosHandler(
    IDailyRoutineRepository routines,
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    IBusinessDateProvider dates)
{
    public async Task<Result<GenerateDailyRoutineTodosResult>> HandleAsync(
        GenerateDailyRoutineTodosCommand command,
        CancellationToken cancellationToken)
    {
        var businessDate = command.BusinessDate ?? dates.Today;
        var dueRoutines = await routines.ListDueForGenerationAsync(
            businessDate,
            cancellationToken);
        var generated = 0;
        var skipped = 0;

        foreach (var routine in dueRoutines)
        {
            if (await routines.GeneratedTodoExistsAsync(
                    routine.Id,
                    businessDate,
                    cancellationToken))
            {
                skipped++;
                continue;
            }

            await todos.AddAsync(
                routine.GenerateTodo(
                    identifiers.NewId(),
                    businessDate,
                    clock.UtcNow),
                cancellationToken);
            generated++;
        }

        if (generated > 0 || skipped > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<GenerateDailyRoutineTodosResult>.Success(
            new GenerateDailyRoutineTodosResult(
                generated,
                skipped,
                businessDate));
    }
}

file static class PersonalTodoHandlerHelpers
{
    public static PersonalTodoDto ToDto(PersonalTodo todo) =>
        PersonalTodoMapping.ToDto(todo);

    public static DailyRoutineDto ToDto(DailyRoutine routine) =>
        new(
            routine.Id,
            routine.Title,
            routine.Notes,
            routine.Priority,
            routine.StartDate,
            routine.EndDate,
            routine.IsActive,
            routine.LastGeneratedDate,
            routine.CreatedAt,
            routine.UpdatedAt);

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

    public static async Task<Result<DailyRoutine>> GetOwnedRoutineAsync(
        IDailyRoutineRepository routines,
        ICurrentUser currentUser,
        Guid routineId,
        CancellationToken cancellationToken)
    {
        var authorization = RequireAuthenticatedUser(currentUser);
        if (!authorization.IsSuccess)
        {
            return Result<DailyRoutine>.Failure(authorization.Error);
        }

        var routine = await routines.GetByIdAsync(routineId, cancellationToken);
        if (routine is null || routine.UserId != currentUser.UserId)
        {
            return Result<DailyRoutine>.Failure(new ApplicationError(
                "daily_routine.not_found",
                "The daily routine was not found.",
                ErrorType.NotFound));
        }

        return Result<DailyRoutine>.Success(routine);
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
