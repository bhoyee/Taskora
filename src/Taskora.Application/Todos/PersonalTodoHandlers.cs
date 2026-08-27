using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Notifications;
using TodoApp.Domain.Common;
using TodoApp.Domain.Todos;
using static TodoApp.Application.Todos.PersonalTodoHandlerHelpers;

namespace TodoApp.Application.Todos;

/// <summary>Lists the current user's personal todos for a date, applying daily carry-over first when viewing today.</summary>
public sealed class ListPersonalTodosHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IClock clock,
    IBusinessDateProvider dates,
    IBackgroundEmailDispatcher emailDispatcher,
    GenerateDailyRoutineTodosHandler dailyRoutines,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and validates paging parameters. When the
    /// requested date is today, first generates today's daily-routine todos
    /// and carries over any of the user's incomplete todos from earlier
    /// dates into today (persisting the change and emailing a carry-over
    /// summary if any were moved), then returns the paged, optionally
    /// search-filtered list of todos for the target date.
    /// </summary>
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

    // Emails the current user a summary of the todos just carried over into today, if they have a known email.
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

        emailDispatcher.Dispatch(
            PersonalTodoCarryOverEmailFactory.Build(
                owner,
                items,
                today));
    }
}

/// <summary>Lists the current user's personal todos across an inclusive date range.</summary>
public sealed class ListPersonalTodosForRangeHandler(
    IPersonalTodoRepository todos,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and that the range is not inverted (end date
    /// on or after the start date), then returns all matching todos for the
    /// current user.
    /// </summary>
    public async Task<Result<IReadOnlyList<PersonalTodoDto>>> HandleAsync(
        ListPersonalTodosForRangeQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = RequireAuthenticatedUser(currentUser);
        if (!authorization.IsSuccess)
        {
            return Result<IReadOnlyList<PersonalTodoDto>>.Failure(
                authorization.Error);
        }

        if (query.To < query.From)
        {
            return Validation<IReadOnlyList<PersonalTodoDto>>(
                "End date must be on or after the start date.");
        }

        var items = await todos.ListForUserBetweenAsync(
            currentUser.UserId,
            query.From,
            query.To,
            cancellationToken);

        return Result<IReadOnlyList<PersonalTodoDto>>.Success(
            items.Select(ToDto).ToArray());
    }
}

/// <summary>Creates a new personal todo for the current user.</summary>
public sealed class CreatePersonalTodoHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication, then creates and persists a new todo owned
    /// by the current user, translating domain validation failures into a
    /// validation error.
    /// </summary>
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

/// <summary>Updates an existing personal todo owned by the current user.</summary>
public sealed class UpdatePersonalTodoHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and that the todo exists and belongs to the
    /// current user (via <see cref="GetOwnedTodoAsync"/>), then applies the
    /// update, translating domain validation failures into a validation
    /// error.
    /// </summary>
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

/// <summary>Marks a personal todo as completed.</summary>
public sealed class CompletePersonalTodoHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and ownership of the todo, then marks it
    /// completed with the current timestamp and persists the change.
    /// </summary>
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

/// <summary>Reopens a previously completed personal todo.</summary>
public sealed class ReopenPersonalTodoHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and ownership of the todo, then clears its
    /// completed state with the current timestamp and persists the change.
    /// </summary>
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

/// <summary>Deletes a personal todo owned by the current user.</summary>
public sealed class DeletePersonalTodoHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and ownership of the todo, then removes it.
    /// </summary>
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

/// <summary>Adds a comment to a personal todo.</summary>
public sealed class AddPersonalTodoCommentHandler(
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and ownership of the todo, and that the
    /// comment body is non-empty, then appends the comment and persists it,
    /// translating domain validation failures into a validation error.
    /// </summary>
    public async Task<Result<PersonalTodoDto>> HandleAsync(
        AddPersonalTodoCommentCommand command,
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

        if (string.IsNullOrWhiteSpace(command.Body))
        {
            return Validation<PersonalTodoDto>("Comment text is required.");
        }

        try
        {
            todo.Value.AddComment(
                identifiers.NewId(),
                command.Body,
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

/// <summary>Maps personal todo entities to their DTO representation.</summary>
internal static class PersonalTodoMapping
{
    // Maps a personal todo, including its ordered comments, to its DTO.
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
            todo.CompletedAt,
            todo.Comments
                .OrderBy(comment => comment.CreatedAt)
                .Select(comment => new PersonalTodoCommentDto(
                    comment.Id,
                    comment.TodoId,
                    comment.Body,
                    comment.CreatedAt))
                .ToArray());
}

/// <summary>Lists the current user's daily routines, with paging.</summary>
public sealed class ListDailyRoutinesHandler(
    IDailyRoutineRepository routines,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and validates paging parameters, then returns
    /// the current user's daily routines as a paged result.
    /// </summary>
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

/// <summary>Creates a new daily routine for the current user.</summary>
public sealed class CreateDailyRoutineHandler(
    IDailyRoutineRepository routines,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication, then creates and persists a new daily
    /// routine owned by the current user, translating domain validation
    /// failures into a validation error.
    /// </summary>
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

/// <summary>Updates an existing daily routine owned by the current user.</summary>
public sealed class UpdateDailyRoutineHandler(
    IDailyRoutineRepository routines,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and that the routine exists and belongs to
    /// the current user (via <see cref="GetOwnedRoutineAsync"/>), then
    /// applies the update, translating domain validation failures into a
    /// validation error.
    /// </summary>
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

/// <summary>Deletes a daily routine owned by the current user.</summary>
public sealed class DeleteDailyRoutineHandler(
    IDailyRoutineRepository routines,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and ownership of the routine, then removes
    /// it.
    /// </summary>
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

/// <summary>Generates personal todos from daily routines that are due for a given business date, idempotently.</summary>
public sealed class GenerateDailyRoutineTodosHandler(
    IDailyRoutineRepository routines,
    IPersonalTodoRepository todos,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    IBusinessDateProvider dates)
{
    /// <summary>
    /// For the given business date (defaulting to today), finds all active
    /// daily routines due for generation and, for each one that has not
    /// already produced a todo for that date (checked to keep this
    /// idempotent when called multiple times per day), generates and adds a
    /// new todo. Persists changes only if any todos were generated or
    /// skipped, and returns counts of each plus the business date used.
    /// </summary>
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

/// <summary>Shared mapping, authorization, and mutation helpers local to the personal-todo handlers in this file.</summary>
file static class PersonalTodoHandlerHelpers
{
    // Delegates to the shared personal-todo DTO mapping.
    public static PersonalTodoDto ToDto(PersonalTodo todo) =>
        PersonalTodoMapping.ToDto(todo);

    // Maps a daily routine entity to its DTO.
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

    // Requires the caller to be authenticated before managing personal todos/routines.
    public static Result<bool> RequireAuthenticatedUser(
        ICurrentUser currentUser) =>
        currentUser.IsAuthenticated
            ? Result<bool>.Success(true)
            : Result<bool>.Failure(new ApplicationError(
                "todo.auth_required",
                "Sign in before managing personal todos.",
                ErrorType.Unauthorized));

    // Requires authentication and that the todo exists and is owned by the current user.
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

    // Requires authentication and that the daily routine exists and is owned by the current user.
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

    // Loads an owned todo, applies the given in-place mutation, persists it, and returns its updated DTO.
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

    // Builds a validation-typed failure result, shared by the personal-todo handlers.
    public static Result<T> Validation<T>(string message) =>
        Result<T>.Failure(new ApplicationError(
            "todo.validation",
            message,
            ErrorType.Validation));
}
