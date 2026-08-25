using TodoApp.Domain.Common;

namespace TodoApp.Domain.Tasks.Events;

/// <summary>
/// Raised whenever a <see cref="TaskItem"/>'s status transitions, so interested
/// parts of the system (e.g. notifications, analytics) can react without the
/// <see cref="TaskItem"/> aggregate needing to know about them directly.
/// </summary>
public sealed record TaskStatusChangedDomainEvent(
    Guid TaskId,
    TaskItemStatus PreviousStatus,
    TaskItemStatus CurrentStatus) : IDomainEvent;
