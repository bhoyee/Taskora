namespace TodoApp.Application.Tasks.Lifecycle;

/// <summary>
/// Command requesting that the task identified by <see cref="TaskId"/> be marked complete.
/// </summary>
public sealed record CompleteTaskCommand(Guid TaskId);
