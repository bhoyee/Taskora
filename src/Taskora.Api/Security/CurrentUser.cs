using System.Security.Claims;
using TodoApp.Application.Abstractions;

namespace TodoApp.Api.Security;

internal sealed class CurrentUser(IHttpContextAccessor accessor)
    : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated == true;

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
