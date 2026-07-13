using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

public sealed class GuidIdentifierGenerator : IIdentifierGenerator
{
    public Guid NewId() => Guid.NewGuid();
}
