using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Tasks.Lifecycle;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Maintenance;

/// <summary>
/// Handles <see cref="MoveTaskToReadyCommand"/> by moving a task back to the ready-to-start state.
/// </summary>
public sealed class MoveTaskToReadyHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Applies the domain's move-to-ready rule via <see cref="TaskMutationExecutor"/> and persists
    /// the change. Returns a <see cref="Result{T}"/> with the resulting status on success, or a
    /// failure carrying a not-found, validation, or conflict error.
    /// </summary>
    public Task<Result<TaskItemStatus>> HandleAsync(
        MoveTaskToReadyCommand command,
        CancellationToken cancellationToken) =>
        TaskMutationExecutor.ExecuteAsync(
            command.TaskId,
            tasks,
            unitOfWork,
            task => task.MoveToReady(),
            cancellationToken);
}

/// <summary>
/// Handles <see cref="UpdateTaskCommand"/> by updating a task's title, due date, effort estimate,
/// and sprint assignment.
/// </summary>
public sealed class UpdateTaskHandler(
    ITaskRepository tasks,
    IProjectRepository projects,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Loads the task and, when a sprint is requested, verifies that sprint belongs to the task's
    /// project before applying the rename/reschedule/re-estimate/sprint-assignment changes. Returns
    /// a <see cref="Result{T}"/> with the resulting status on success, or a failure carrying a
    /// not-found, validation, or conflict error.
    /// </summary>
    public async Task<Result<TaskItemStatus>> HandleAsync(
        UpdateTaskCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);
        if (task is null)
        {
            return TaskMutationExecutor.NotFound();
        }

        if (command.SprintId.HasValue)
        {
            var project = await projects.GetByIdAsync(
                task.ProjectId,
                cancellationToken);
            if (project is null || !project.HasSprint(command.SprintId.Value))
            {
                return Result<TaskItemStatus>.Failure(
                    new ApplicationError(
                        "sprint.not_found",
                        "The sprint was not found for this project.",
                        ErrorType.NotFound));
            }
        }

        return await TaskMutationExecutor.ExecuteLoadedAsync(
            task,
            command.TaskId,
            unitOfWork,
            item =>
            {
                item.Rename(command.Title);

                if (command.DueDate.HasValue)
                {
                    item.Schedule(DueDate.Create(command.DueDate.Value));
                }

                if (command.Effort.HasValue)
                {
                    item.Estimate(EffortEstimate.Create(command.Effort.Value));
                }

                item.AssignSprint(command.SprintId);
            },
            cancellationToken);
    }
}

/// <summary>
/// Handles <see cref="BlockTaskCommand"/> by marking a task as blocked with a recorded reason.
/// </summary>
public sealed class BlockTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Loads the task, verifies the current user is the assigned worker (only the assignee may
    /// block their own task), then applies the domain block rule and persists the change. Returns a
    /// <see cref="Result{T}"/> with the resulting status on success, or a failure carrying a
    /// not-found, authorization, validation, or conflict error.
    /// </summary>
    public async Task<Result<TaskItemStatus>> HandleAsync(
        BlockTaskCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);
        if (task is null)
        {
            return TaskMutationExecutor.NotFound();
        }

        var authorization = AssignedTaskAuthorization.EnsureAssignedWorker(
            task,
            currentUser);
        if (!authorization.IsSuccess)
        {
            return Result<TaskItemStatus>.Failure(authorization.Error);
        }

        return await TaskMutationExecutor.ExecuteLoadedAsync(
            task,
            command.TaskId,
            unitOfWork,
            item => item.Block(command.Reason),
            cancellationToken);
    }
}

/// <summary>
/// Handles <see cref="UnblockTaskCommand"/> by clearing a task's blocked status, restricting who may
/// do so to the assignee, creator, or a workspace owner/manager.
/// </summary>
public sealed class UnblockTaskHandler(
    ITaskRepository tasks,
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Loads the task, verifies the current user is authorized to clear its blocker (see
    /// <see cref="EnsureCanClearBlockerAsync"/>), then applies the domain unblock rule and persists
    /// the change. Returns a <see cref="Result{T}"/> with the resulting status on success, or a
    /// failure carrying a not-found, authorization, validation, or conflict error.
    /// </summary>
    public async Task<Result<TaskItemStatus>> HandleAsync(
        UnblockTaskCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);
        if (task is null)
        {
            return TaskMutationExecutor.NotFound();
        }

        var authorization = await EnsureCanClearBlockerAsync(
            task,
            cancellationToken);
        if (!authorization.IsSuccess)
        {
            return Result<TaskItemStatus>.Failure(authorization.Error);
        }

        return await TaskMutationExecutor.ExecuteLoadedAsync(
            task,
            command.TaskId,
            unitOfWork,
            item => item.Unblock(),
            cancellationToken);
    }

    // Authorizes clearing a task's blocker: allowed for the assignee, the creator, or a workspace
    // owner/manager; denied otherwise (including when the user is not signed in, or the project or
    // workspace cannot be resolved).
    private async Task<Result<bool>> EnsureCanClearBlockerAsync(
        TaskItem task,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result<bool>.Failure(new ApplicationError(
                "task.auth_required",
                "Sign in before changing active task status.",
                ErrorType.Unauthorized));
        }

        if (task.AssignedUserId == currentUser.UserId ||
            task.CreatedByUserId == currentUser.UserId)
        {
            return Result<bool>.Success(true);
        }

        var project = await projects.GetByIdAsync(
            task.ProjectId,
            cancellationToken);
        if (project is null || project.WorkspaceId == Guid.Empty)
        {
            return Result<bool>.Failure(new ApplicationError(
                "task.unblock_forbidden",
                "Only the task assignee, creator, workspace owner, or workspace manager can clear a blocker.",
                ErrorType.Forbidden));
        }

        var workspace = await workspaces.GetByIdAsync(
            project.WorkspaceId,
            cancellationToken);
        if (workspace is null || !workspace.HasMember(currentUser.UserId))
        {
            return Result<bool>.Failure(new ApplicationError(
                "workspace.not_found",
                "The workspace was not found.",
                ErrorType.NotFound));
        }

        var role = workspace.GetRole(currentUser.UserId);
        if (role is WorkspaceRole.Owner or WorkspaceRole.Manager)
        {
            return Result<bool>.Success(true);
        }

        return Result<bool>.Failure(new ApplicationError(
            "task.unblock_forbidden",
            "Only the task assignee, creator, workspace owner, or workspace manager can clear a blocker.",
            ErrorType.Forbidden));
    }
}

/// <summary>
/// Handles <see cref="ResumeTaskCommand"/> by resuming a paused task's active work.
/// </summary>
public sealed class ResumeTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Loads the task, verifies the current user is the assigned worker (only the assignee may
    /// resume their own task), then applies the domain resume rule and persists the change. Returns
    /// a <see cref="Result{T}"/> with the resulting status on success, or a failure carrying a
    /// not-found, authorization, validation, or conflict error.
    /// </summary>
    public async Task<Result<TaskItemStatus>> HandleAsync(
        ResumeTaskCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);
        if (task is null)
        {
            return TaskMutationExecutor.NotFound();
        }

        var authorization = AssignedTaskAuthorization.EnsureAssignedWorker(
            task,
            currentUser);
        if (!authorization.IsSuccess)
        {
            return Result<TaskItemStatus>.Failure(authorization.Error);
        }

        return await TaskMutationExecutor.ExecuteLoadedAsync(
            task,
            command.TaskId,
            unitOfWork,
            item => item.Resume(),
            cancellationToken);
    }
}

/// <summary>
/// Handles <see cref="ReopenTaskCommand"/> by reopening a completed or closed task.
/// </summary>
public sealed class ReopenTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Applies the domain's reopen rule via <see cref="TaskMutationExecutor"/> and persists the
    /// change. Returns a <see cref="Result{T}"/> with the resulting status on success, or a failure
    /// carrying a not-found, validation, or conflict error.
    /// </summary>
    public Task<Result<TaskItemStatus>> HandleAsync(
        ReopenTaskCommand command,
        CancellationToken cancellationToken) =>
        TaskMutationExecutor.ExecuteAsync(
            command.TaskId,
            tasks,
            unitOfWork,
            task => task.Reopen(),
            cancellationToken);
}

/// <summary>
/// Handles <see cref="DeleteTaskCommand"/> by permanently removing a task, restricted to the task's
/// creator.
/// </summary>
public sealed class DeleteTaskHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Loads the task, verifies the current user is signed in and is the task's creator, then
    /// removes the task and persists the change. Returns a <see cref="Result{T}"/> of
    /// <see langword="true"/> on success, or a failure carrying a not-found or forbidden error.
    /// </summary>
    public async Task<Result<bool>> HandleAsync(
        DeleteTaskCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);

        if (task is null)
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "task.not_found",
                    "The task was not found.",
                    ErrorType.NotFound));
        }

        if (!currentUser.IsAuthenticated ||
            task.CreatedByUserId is null ||
            task.CreatedByUserId != currentUser.UserId)
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "task.delete_forbidden",
                    "Only the task creator can delete this task.",
                    ErrorType.Forbidden));
        }

        await tasks.RemoveAsync(task, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

/// <summary>
/// Handles <see cref="UpdatePlanningFactorsCommand"/> by updating a task's prioritization inputs,
/// restricted to the task's creator.
/// </summary>
public sealed class UpdatePlanningFactorsHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Loads the task, verifies the current user is signed in and (when the task has a recorded
    /// creator) is that creator, then applies the new planning factors and persists the change.
    /// Returns a <see cref="Result{T}"/> with the resulting status on success, or a failure carrying
    /// a not-found, forbidden, validation, or conflict error.
    /// </summary>
    public async Task<Result<TaskItemStatus>> HandleAsync(
        UpdatePlanningFactorsCommand command,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(command.TaskId, cancellationToken);

        if (task is null)
        {
            return Result<TaskItemStatus>.Failure(
                new ApplicationError(
                    "task.not_found",
                    "The task was not found.",
                    ErrorType.NotFound));
        }

        if (!currentUser.IsAuthenticated ||
            (task.CreatedByUserId is not null &&
             task.CreatedByUserId != currentUser.UserId))
        {
            return Result<TaskItemStatus>.Failure(
                new ApplicationError(
                    "task.planning_forbidden",
                    "Only the task creator can edit priority inputs.",
                    ErrorType.Forbidden));
        }

        return await TaskMutationExecutor.ExecuteLoadedAsync(
            task,
            command.TaskId,
            unitOfWork,
            item => item.SetPlanningFactors(
                PlanningFactors.Create(
                    command.BusinessValue,
                    command.Urgency,
                    command.RiskReduction,
                    command.Effort)),
            cancellationToken);
    }
}

/// <summary>
/// Handles <see cref="RemoveTaskDependencyCommand"/> by removing a dependency link from a task.
/// </summary>
public sealed class RemoveTaskDependencyHandler(
    ITaskRepository tasks,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Applies the domain's remove-dependency rule via <see cref="TaskMutationExecutor"/> and
    /// persists the change. Returns a <see cref="Result{T}"/> with the resulting status on success,
    /// or a failure carrying a not-found, validation, or conflict error.
    /// </summary>
    public Task<Result<TaskItemStatus>> HandleAsync(
        RemoveTaskDependencyCommand command,
        CancellationToken cancellationToken) =>
        TaskMutationExecutor.ExecuteAsync(
            command.TaskId,
            tasks,
            unitOfWork,
            task => task.RemoveDependency(command.DependencyId),
            cancellationToken);
}
