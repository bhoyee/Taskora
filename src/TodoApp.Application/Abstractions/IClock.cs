namespace TodoApp.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
