using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Lifecycle;

/// <summary>
/// Command requesting a direct transition of the task identified by <see cref="TaskId"/> to
/// <see cref="Status"/>, with an optional <see cref="BlockedReason"/> when moving to a blocked state.
/// </summary>
public sealed record SetTaskStatusCommand(
    Guid TaskId,
    TaskItemStatus Status,
    string? BlockedReason = null);
