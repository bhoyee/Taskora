namespace TodoApp.Application.Abstractions;

/// <summary>
/// Provides access to the identity of the user making the current request.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Gets a value indicating whether the current request is associated with an authenticated user.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Gets the identifier of the currently authenticated user.</summary>
    Guid UserId { get; }
}
