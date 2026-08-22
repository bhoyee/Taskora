using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Lifecycle;

public sealed record SetTaskStatusCommand(
    Guid TaskId,
    TaskItemStatus Status,
    string? BlockedReason = null);
