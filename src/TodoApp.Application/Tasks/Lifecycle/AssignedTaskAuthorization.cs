using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Lifecycle;

internal static class AssignedTaskAuthorization
{
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
