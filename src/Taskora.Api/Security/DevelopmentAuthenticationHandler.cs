using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TodoApp.Api.Security;

/// <summary>
/// Development-only authentication scheme that trusts the caller's identity
/// from a plain header/bearer value instead of validating a real token or
/// cookie. Intended solely for local development/testing (wired up
/// conditionally in the security module) and must never be enabled in
/// production, since it performs no credential verification.
/// </summary>
internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options,
        logger,
        encoder)
{
    public const string SchemeName = "Development";
    public const string UserHeader = "X-User-Id";

    /// <summary>
    /// Authenticates the request by reading a user id directly from either
    /// the <c>X-User-Id</c> header or an "Authorization: Bearer {guid}" header
    /// (no signature/token validation is performed — the GUID is trusted
    /// as-is), building a <see cref="ClaimsPrincipal"/> with just a
    /// <see cref="ClaimTypes.NameIdentifier"/> claim. Returns
    /// <see cref="AuthenticateResult.NoResult"/> when neither header supplies
    /// a valid GUID, allowing the request to be treated as unauthenticated.
    /// </summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Guid userId;
        if (Request.Headers.TryGetValue(UserHeader, out var value) &&
            Guid.TryParse(value, out userId))
        {
        }
        else if (Request.Headers.Authorization.ToString()
                     .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
                 Guid.TryParse(
                     Request.Headers.Authorization.ToString()["Bearer ".Length..],
                     out userId))
        {
        }
        else
        {
            return Task.FromResult(
                AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            SchemeName);
        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(
                    new ClaimsPrincipal(identity),
                    SchemeName)));
    }
}
