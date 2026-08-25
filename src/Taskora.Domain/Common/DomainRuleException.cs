namespace TodoApp.Domain.Common;

/// <summary>
/// Thrown when an operation would violate a domain business rule or invariant
/// (as opposed to invalid input data — see <see cref="DomainValidationException"/>).
/// </summary>
public sealed class DomainRuleException(string message) : InvalidOperationException(message);
