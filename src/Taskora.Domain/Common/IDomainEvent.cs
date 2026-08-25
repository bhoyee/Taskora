namespace TodoApp.Domain.Common;

/// <summary>
/// Marker interface for domain events raised by aggregates to signal something
/// meaningful happened, so other parts of the system can react to it.
/// </summary>
public interface IDomainEvent;
