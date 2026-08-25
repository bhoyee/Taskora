namespace TodoApp.Application.Tasks.Lifecycle;

/// <summary>
/// Command requesting that <see cref="DependencyId"/> be added as a dependency of <see cref="TaskId"/>.
/// </summary>
public sealed record AddTaskDependencyCommand(
    Guid TaskId,
    Guid DependencyId);
