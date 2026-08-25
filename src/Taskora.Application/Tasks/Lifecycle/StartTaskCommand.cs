namespace TodoApp.Application.Tasks.Lifecycle;

/// <summary>
/// Command requesting that the task identified by <see cref="TaskId"/> be started.
/// </summary>
public sealed record StartTaskCommand(Guid TaskId);
