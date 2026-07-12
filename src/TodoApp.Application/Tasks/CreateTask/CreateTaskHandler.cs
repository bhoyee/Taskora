using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.CreateTask;

public sealed class CreateTaskHandler(
    IProjectRepository projects,
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<TaskDto>> HandleAsync(
        CreateTaskCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(
            command.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return Result<TaskDto>.Failure(
                new ApplicationError(
                    "project.not_found",
                    "The project was not found.",
                    ErrorType.NotFound));
        }

        try
        {
            project.EnsureCanAcceptTasks();

            var task = TaskItem.Create(
                identifiers.NewId(),
                project.Id,
                command.Title,
                clock.UtcNow);
            if (currentUser.IsAuthenticated)
            {
                task.RecordCreator(currentUser.UserId);
            }

            if (command.DueDate.HasValue)
            {
                task.Schedule(DueDate.Create(command.DueDate.Value));
            }

            if (command.Effort.HasValue)
            {
                task.Estimate(EffortEstimate.Create(command.Effort.Value));
            }

            await tasks.AddAsync(task, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<TaskDto>.Success(ToDto(task));
        }
        catch (DomainValidationException exception)
        {
            return Result<TaskDto>.Failure(
                new ApplicationError(
                    "task.validation",
                    exception.Message,
                    ErrorType.Validation));
        }
        catch (DomainRuleException exception)
        {
            return Result<TaskDto>.Failure(
                new ApplicationError(
                    "task.conflict",
                    exception.Message,
                    ErrorType.Conflict));
        }
    }

    private static TaskDto ToDto(TaskItem task) =>
        new(
            task.Id,
            task.ProjectId,
            task.CreatedByUserId,
            task.Title,
            task.Status,
            task.DueDate?.Value,
            task.EffortEstimate?.Value);
}
