using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Notifications;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Application.Tasks.Assignment;

/// <summary>
/// Command requesting that the task identified by <see cref="TaskId"/> be assigned to
/// <see cref="UserId"/>.
/// </summary>
public sealed record AssignTaskCommand(Guid TaskId, Guid UserId);

/// <summary>
/// Command requesting that the task identified by <see cref="TaskId"/> be unassigned.
/// </summary>
public sealed record UnassignTaskCommand(Guid TaskId);

/// <summary>
/// Handles <see cref="AssignTaskCommand"/> by assigning a task to a workspace member, restricted to
/// workspace owners/managers, and notifying the new assignee by email.
/// </summary>
public sealed class AssignTaskHandler(
    ITaskRepository tasks,
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUserProfileRepository users,
    INotificationEmailSender emailSender,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Loads the task, its project, and workspace, verifying the current user is an active,
    /// non-suspended workspace owner or manager and that the requested assignee is a workspace
    /// member. On success, assigns the task, persists the change, and best-effort emails the new
    /// assignee a notification. Returns a <see cref="Result{T}"/> of <see langword="true"/> on
    /// success, or a failure carrying a not-found, forbidden, or validation error.
    /// </summary>
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
            workspace.IsSuspended ||
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
                TaskoraEmailTemplate.Build(
                    [user.Email],
                    $"New task assigned: {task.Title}",
                    "Task assignment",
                    "You have been assigned a task",
                    $"Hello {user.DisplayName},",
                    $"You have been assigned a task in {projectName}.",
                    [
                        new EmailDetail("Project", projectName),
                        new EmailDetail("Task", task.Title),
                        new EmailDetail("Due date", task.DueDate?.Value.ToString("yyyy-MM-dd") ?? "Not set")
                    ],
                    "Please sign in to Taskora to review the details."),
                cancellationToken);
        }

        return Result<bool>.Success(true);
    }

    // Standard "task not found" failure, shared with UnassignTaskHandler.
    internal static Result<bool> NotFound() =>
        Result<bool>.Failure(new ApplicationError(
            "task.not_found", "The task was not found.", ErrorType.NotFound));

    // Standard "insufficient role" failure, shared with UnassignTaskHandler.
    internal static Result<bool> Forbidden() =>
        Result<bool>.Failure(new ApplicationError(
            "assignment.forbidden",
            "Manager or owner access is required.",
            ErrorType.Forbidden));
}

/// <summary>
/// Handles <see cref="UnassignTaskCommand"/> by clearing a task's assignee, restricted to active,
/// non-suspended workspace owners/managers.
/// </summary>
public sealed class UnassignTaskHandler(
    ITaskRepository tasks,
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Loads the task, its project, and workspace, verifying the current user is an active,
    /// non-suspended workspace owner or manager, then unassigns the task and persists the change.
    /// Returns a <see cref="Result{T}"/> of <see langword="true"/> on success, or a failure carrying
    /// a not-found or forbidden error.
    /// </summary>
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
            workspace.IsSuspended ||
            workspace.GetRole(currentUser.UserId) == WorkspaceRole.Member)
        {
            return AssignTaskHandler.Forbidden();
        }

        task.Unassign();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
