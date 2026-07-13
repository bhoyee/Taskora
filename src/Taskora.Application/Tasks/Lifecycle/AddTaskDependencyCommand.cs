namespace TodoApp.Application.Tasks.Lifecycle;

public sealed record AddTaskDependencyCommand(
    Guid TaskId,
    Guid DependencyId);
