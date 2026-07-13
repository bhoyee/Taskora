using TodoApp.Application.Common;
using TodoApp.Domain.Common;

namespace TodoApp.Application.Tasks.Lifecycle;

internal static class TaskOperationErrors
{
    public static ApplicationError TaskNotFound() =>
        new(
            "task.not_found",
            "The task was not found.",
            ErrorType.NotFound);

    public static ApplicationError DependencyNotFound() =>
        new(
            "task.dependency_not_found",
            "The dependency task was not found.",
            ErrorType.NotFound);

    public static ApplicationError From(DomainRuleException exception) =>
        new("task.conflict", exception.Message, ErrorType.Conflict);

    public static ApplicationError From(DomainValidationException exception) =>
        new("task.validation", exception.Message, ErrorType.Validation);
}
