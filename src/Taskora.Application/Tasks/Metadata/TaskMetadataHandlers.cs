using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;

namespace TodoApp.Application.Tasks.Metadata;

public sealed class CreateCategoryHandler(
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers)
{
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

    internal static ApplicationError NotFound(string resource) =>
        new($"{resource}.not_found", $"The {resource} was not found.", ErrorType.NotFound);

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

public sealed class UpdateTaskCategoryHandler(
    ITaskRepository tasks,
    IProjectRepository projects,
    IUnitOfWork unitOfWork)
{
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

public sealed class AddTaskTagHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
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

public sealed class RemoveTaskTagHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
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

public sealed class AddTaskNoteHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    ICurrentUser currentUser)
{
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
