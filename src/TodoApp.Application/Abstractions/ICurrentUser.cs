namespace TodoApp.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }
}
