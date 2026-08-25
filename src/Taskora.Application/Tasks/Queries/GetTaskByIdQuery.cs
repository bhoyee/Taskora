namespace TodoApp.Application.Tasks.Queries;

/// <summary>
/// Query requesting the full details of the task identified by <paramref name="TaskId"/>.
/// </summary>
public sealed record GetTaskByIdQuery(Guid TaskId);
