namespace TodoApp.Application.Abstractions;

/// <summary>
/// Abstraction for generating new unique identifiers for entities, allowing the
/// generation strategy to be swapped or substituted (e.g. in tests).
/// </summary>
public interface IIdentifierGenerator
{
    /// <summary>Generates a new, unique identifier.</summary>
    /// <returns>A new <see cref="Guid"/> suitable for use as an entity identifier.</returns>
    Guid NewId();
}
