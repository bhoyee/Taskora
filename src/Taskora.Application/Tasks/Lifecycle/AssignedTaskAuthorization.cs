using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Lifecycle;

/// <summary>
/// Shared authorization checks used by task lifecycle operations that require the caller to be
/// signed in and either unassigned-and-eligible or the assigned worker on the task.
/// </summary>
internal static class AssignedTaskAuthorization
{
    /// <summary>
    /// Ensures the current user is authenticated and, if the task already has an assignee, that the
    /// assignee is the current user - i.e. a task assigned to someone else cannot be started/picked
    /// up by another user. Returns a failed <see cref="Result{T}"/> with an unauthorized or forbidden
    /// error when the check fails, otherwise a successful result.
    /// </summary>
    public static Result<bool> EnsureCanStart(
        TaskItem task,
        ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result<bool>.Failure(new ApplicationError(
                "task.auth_required",
                "Sign in before changing active task status.",
                ErrorType.Unauthorized));
        }

        if (task.AssignedUserId is not null &&
            task.AssignedUserId != currentUser.UserId)
        {
            return Result<bool>.Failure(new ApplicationError(
                "task.assignee_required",
                "This task is already assigned and is not available to pick up.",
                ErrorType.Forbidden));
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Ensures the current user is authenticated, the task has an assignee, and that assignee is the
    /// current user - used to gate operations (block/unblock/complete) that only the person actively
    /// working the task should be able to perform. Returns a failed <see cref="Result{T}"/> with an
    /// unauthorized, conflict (unassigned), or forbidden error when the check fails, otherwise a
    /// successful result.
    /// </summary>
    public static Result<bool> EnsureAssignedWorker(
        TaskItem task,
        ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result<bool>.Failure(new ApplicationError(
                "task.auth_required",
                "Sign in before changing active task status.",
                ErrorType.Unauthorized));
        }

        if (task.AssignedUserId is null)
        {
            return Result<bool>.Failure(new ApplicationError(
                "task.assignment_required",
                "Assign the task to a workspace member before starting active work.",
                ErrorType.Conflict));
        }

        if (task.AssignedUserId != currentUser.UserId)
        {
            return Result<bool>.Failure(new ApplicationError(
                "task.assignee_required",
                "Only the assigned user can block, unblock, or complete this task.",
                ErrorType.Forbidden));
        }

        return Result<bool>.Success(true);
    }
}
