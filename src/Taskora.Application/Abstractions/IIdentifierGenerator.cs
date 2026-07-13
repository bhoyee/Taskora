namespace TodoApp.Application.Abstractions;

public interface IIdentifierGenerator
{
    Guid NewId();
}
