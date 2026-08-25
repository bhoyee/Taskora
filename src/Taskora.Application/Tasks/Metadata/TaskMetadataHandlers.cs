using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;

namespace TodoApp.Application.Tasks.Metadata;

/// <summary>
/// Handles creation of a new category on a project.
/// </summary>
public sealed class CreateCategoryHandler(
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers)
{
    /// <summary>
    /// Loads the target project, adds a new category with a generated identifier and
    /// the requested name, persists the change, and returns the created category as a
    /// <see cref="ProjectCategoryDto"/>. Fails with not-found if the project does not exist.
    /// </summary>
    public async Task<Result<ProjectCategoryDto>> HandleAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(
            command.ProjectId,
            cancellationToken);
        if (project is null)
        {
            return Result<ProjectCategoryDto>.Failure(NotFound("project"));
        }

        return await ExecuteAsync(() =>
        {
            var category = project.AddCategory(
                identifiers.NewId(),
                command.Name);
            return new ProjectCategoryDto(
                category.Id,
                category.ProjectId,
                category.Name);
        }, unitOfWork, cancellationToken);
    }

    // Builds a standard not-found ApplicationError for the named resource, shared by
    // all metadata handlers below.
    internal static ApplicationError NotFound(string resource) =>
        new($"{resource}.not_found", $"The {resource} was not found.", ErrorType.NotFound);

    // Runs a domain mutation, persists via the unit of work, and translates any domain
    // validation/rule exceptions into a failed Result. Shared by all metadata handlers.
    internal static async Task<Result<T>> ExecuteAsync<T>(
        Func<T> operation,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = operation();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<T>.Success(value);
        }
        catch (DomainValidationException exception)
        {
            return Result<T>.Failure(
                new ApplicationError(
                    "metadata.validation",
                    exception.Message,
                    ErrorType.Validation));
        }
        catch (DomainRuleException exception)
        {
            return Result<T>.Failure(
                new ApplicationError(
                    "metadata.conflict",
                    exception.Message,
                    ErrorType.Conflict));
        }
    }
}

/// <summary>
/// Handles assigning or clearing a task's category.
/// </summary>
public sealed class UpdateTaskCategoryHandler(
    ITaskRepository tasks,
    IProjectRepository projects,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Loads the task and its owning project, validates that the requested category
    /// (if any) actually belongs to that project, then assigns the category and
    /// persists the change. Fails with not-found if the task, project, or category
    /// cannot be resolved. Returns the resulting category id (or null if cleared).
    /// </summary>
    public async Task<Result<Guid?>> HandleAsync(
        UpdateTaskCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);
        if (task is null)
        {
            return Result<Guid?>.Failure(CreateCategoryHandler.NotFound("task"));
        }

        var project = await projects.GetByIdAsync(
            task.ProjectId,
            cancellationToken);
        if (project is null)
        {
            return Result<Guid?>.Failure(CreateCategoryHandler.NotFound("project"));
        }

        if (command.CategoryId.HasValue &&
            !project.HasCategory(command.CategoryId.Value))
        {
            return Result<Guid?>.Failure(CreateCategoryHandler.NotFound("category"));
        }

        return await CreateCategoryHandler.ExecuteAsync(
            () =>
            {
                task.AssignCategory(command.CategoryId);
                return task.CategoryId;
            },
            unitOfWork,
            cancellationToken);
    }
}

/// <summary>
/// Handles adding a tag to a task.
/// </summary>
public sealed class AddTaskTagHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Loads the task, adds the requested tag, persists the change, and returns the
    /// task's full updated tag list. Fails with not-found if the task does not exist.
    /// </summary>
    public async Task<Result<IReadOnlyCollection<string>>> HandleAsync(
        AddTaskTagCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);
        if (task is null)
        {
            return Result<IReadOnlyCollection<string>>.Failure(
                CreateCategoryHandler.NotFound("task"));
        }

        return await CreateCategoryHandler.ExecuteAsync(
            () =>
            {
                task.AddTag(command.Name);
                return (IReadOnlyCollection<string>)task.Tags
                    .Select(tag => tag.Name)
                    .ToArray();
            },
            unitOfWork,
            cancellationToken);
    }
}

/// <summary>
/// Handles removing a tag from a task.
/// </summary>
public sealed class RemoveTaskTagHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Loads the task, removes the requested tag, persists the change, and returns
    /// the task's full updated tag list. Fails with not-found if the task does not exist.
    /// </summary>
    public async Task<Result<IReadOnlyCollection<string>>> HandleAsync(
        RemoveTaskTagCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);
        if (task is null)
        {
            return Result<IReadOnlyCollection<string>>.Failure(
                CreateCategoryHandler.NotFound("task"));
        }

        return await CreateCategoryHandler.ExecuteAsync(
            () =>
            {
                task.RemoveTag(command.Name);
                return (IReadOnlyCollection<string>)task.Tags
                    .Select(tag => tag.Name)
                    .ToArray();
            },
            unitOfWork,
            cancellationToken);
    }
}

/// <summary>
/// Handles adding a note to a task on behalf of the current user.
/// </summary>
public sealed class AddTaskNoteHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires an authenticated current user, loads the target task, appends a new
    /// note authored by that user (with a generated id and the current UTC timestamp),
    /// persists the change, and returns the created note as a <see cref="TaskNoteDto"/>.
    /// Fails with unauthorized if no user is authenticated, or not-found if the task
    /// does not exist.
    /// </summary>
    public async Task<Result<TaskNoteDto>> HandleAsync(
        AddTaskNoteCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result<TaskNoteDto>.Failure(
                new ApplicationError(
                    "auth.required",
                    "Authentication is required.",
                    ErrorType.Unauthorized));
        }

        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);
        if (task is null)
        {
            return Result<TaskNoteDto>.Failure(CreateCategoryHandler.NotFound("task"));
        }

        return await CreateCategoryHandler.ExecuteAsync(
            () =>
            {
                var note = task.AddNote(
                    identifiers.NewId(),
                    currentUser.UserId,
                    command.Body,
                    clock.UtcNow);
                return new TaskNoteDto(
                    note.Id,
                    note.TaskId,
                    note.AuthorId,
                    note.Body,
                    note.CreatedAt);
            },
            unitOfWork,
            cancellationToken);
    }
}
