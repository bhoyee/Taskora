namespace TodoApp.Application.Abstractions;

/// <summary>
/// Abstraction over the system clock, allowing the current time to be
/// substituted (e.g. in tests) instead of using <see cref="DateTimeOffset.UtcNow"/> directly.
/// </summary>
public interface IClock
{
    /// <summary>Gets the current date and time in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
