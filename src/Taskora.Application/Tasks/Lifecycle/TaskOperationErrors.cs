using TodoApp.Application.Common;
using TodoApp.Domain.Common;

namespace TodoApp.Application.Tasks.Lifecycle;

/// <summary>
/// Factory helpers for the <see cref="ApplicationError"/>s commonly returned by task lifecycle
/// command handlers, keeping error codes and messages consistent across handlers.
/// </summary>
internal static class TaskOperationErrors
{
    // Error returned when the target task could not be found.
    public static ApplicationError TaskNotFound() =>
        new(
            "task.not_found",
            "The task was not found.",
            ErrorType.NotFound);

    // Error returned when a referenced dependency task could not be found.
    public static ApplicationError DependencyNotFound() =>
        new(
            "task.dependency_not_found",
            "The dependency task was not found.",
            ErrorType.NotFound);

    // Maps a domain rule violation (e.g. an invalid state transition) to a conflict error.
    public static ApplicationError From(DomainRuleException exception) =>
        new("task.conflict", exception.Message, ErrorType.Conflict);

    // Maps a domain validation failure (e.g. bad input data) to a validation error.
    public static ApplicationError From(DomainValidationException exception) =>
        new("task.validation", exception.Message, ErrorType.Validation);
}
