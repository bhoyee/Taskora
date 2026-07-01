namespace TodoApp.Domain.Common;

public sealed class DomainRuleException(string message) : InvalidOperationException(message);
