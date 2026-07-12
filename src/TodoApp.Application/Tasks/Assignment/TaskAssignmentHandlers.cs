using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Application.Tasks.Assignment;

public sealed record AssignTaskCommand(Guid TaskId, Guid UserId);

public sealed record UnassignTaskCommand(Guid TaskId);

public sealed class AssignTaskHandler(
    ITaskRepository tasks,
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUserProfileRepository users,
    INotificationEmailSender emailSender,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(
        AssignTaskCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);
        if (task is null) return NotFound();
        var project = await projects.GetByIdAsync(
            task.ProjectId, cancellationToken);
        var workspace = project is null
            ? null
            : await workspaces.GetByIdAsync(
                project.WorkspaceId, cancellationToken);
        if (project is null ||
            workspace is null ||
            !workspace.HasMember(currentUser.UserId) ||
            workspace.GetRole(currentUser.UserId) == WorkspaceRole.Member)
        {
            return Forbidden();
        }

        if (!workspace.HasMember(command.UserId))
        {
            return Result<bool>.Failure(new ApplicationError(
                "assignment.invalid_user",
                "Tasks can only be assigned to workspace members.",
                ErrorType.Validation));
        }

        task.Assign(command.UserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var projectName = project.Name;
        var assignee = await users.GetByIdsAsync([command.UserId], cancellationToken);
        var user = assignee.SingleOrDefault();
        if (user is not null)
        {
            await emailSender.SendAsync(
                new NotificationEmailMessage(
                    [user.Email],
                    $"New task assigned: {task.Title}",
                    $"""
                    Hello {user.DisplayName},

                    You have been assigned a task in {projectName}.

                    Task: {task.Title}
                    Due date: {(task.DueDate?.Value.ToString("yyyy-MM-dd") ?? "Not set")}

                    Please sign in to Todo Intelligence to review the details.
                    """),
                cancellationToken);
        }

        return Result<bool>.Success(true);
    }

    internal static Result<bool> NotFound() =>
        Result<bool>.Failure(new ApplicationError(
            "task.not_found", "The task was not found.", ErrorType.NotFound));

    internal static Result<bool> Forbidden() =>
        Result<bool>.Failure(new ApplicationError(
            "assignment.forbidden",
            "Manager or owner access is required.",
            ErrorType.Forbidden));
}

public sealed class UnassignTaskHandler(
    ITaskRepository tasks,
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(
        UnassignTaskCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);
        if (task is null) return AssignTaskHandler.NotFound();
        var project = await projects.GetByIdAsync(
            task.ProjectId, cancellationToken);
        var workspace = project is null
            ? null
            : await workspaces.GetByIdAsync(
                project.WorkspaceId, cancellationToken);
        if (workspace is null ||
            !workspace.HasMember(currentUser.UserId) ||
            workspace.GetRole(currentUser.UserId) == WorkspaceRole.Member)
        {
            return AssignTaskHandler.Forbidden();
        }

        task.Unassign();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
