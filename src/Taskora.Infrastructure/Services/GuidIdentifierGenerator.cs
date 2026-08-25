using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

/// <summary>Default <see cref="IIdentifierGenerator"/> that produces new random GUIDs for entity identifiers.</summary>
public sealed class GuidIdentifierGenerator : IIdentifierGenerator
{
    /// <summary>Generates a new random GUID.</summary>
    public Guid NewId() => Guid.NewGuid();
}
