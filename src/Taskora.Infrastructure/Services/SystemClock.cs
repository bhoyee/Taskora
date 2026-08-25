using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

/// <summary>
/// Default <see cref="IClock"/> backed by the system clock. Wrapping
/// <see cref="DateTimeOffset.UtcNow"/> behind this abstraction lets
/// application/domain code depend on <see cref="IClock"/> instead, so time
/// can be faked in tests.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <summary>The current UTC time, as reported by the system clock.</summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
