using System.Security.Claims;
using TodoApp.Application.Abstractions;

namespace TodoApp.Api.Security;

/// <summary>
/// Resolves the current authenticated user's identity from the ambient HTTP
/// context's claims principal, for use outside of endpoint handlers (e.g. in
/// application-layer handlers that need to know who is acting).
/// </summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor)
    : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    /// <summary>Whether the current HTTP request has an authenticated identity.</summary>
    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated == true;

    /// <summary>
    /// The current user's id, parsed from the standard name-identifier claim.
    /// Returns <see cref="Guid.Empty"/> if there is no request, no claim, or the
    /// claim value isn't a valid GUID.
    /// </summary>
    public Guid UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(
                ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId)
                ? userId
                : Guid.Empty;
        }
    }
}
