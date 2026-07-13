namespace TodoApp.Domain.Common;

public sealed class DomainValidationException(string message) : ArgumentException(message);
