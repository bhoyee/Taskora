namespace TodoApp.Domain.Common;

/// <summary>
/// Thrown when input supplied to a domain entity or value object is structurally invalid
/// (e.g. missing required data), as opposed to a business rule violation — see
/// <see cref="DomainRuleException"/>.
/// </summary>
public sealed class DomainValidationException(string message) : ArgumentException(message);
