using TodoApp.Domain.Common;

namespace TodoApp.Domain.Tasks.Events;

public sealed record TaskStatusChangedDomainEvent(
    Guid TaskId,
    TaskItemStatus PreviousStatus,
    TaskItemStatus CurrentStatus) : IDomainEvent;
