using Microsoft.Extensions.Options;
using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

/// <summary>
/// Builds absolute, user-facing URLs into the application (e.g. links sent
/// in emails) using the configured public base URL rather than the
/// server's internal request URL.
/// </summary>
public sealed class ApplicationLinkBuilder(
    IOptions<ApplicationUrlOptions> options)
    : IApplicationLinkBuilder
{
    /// <summary>Builds the public URL a recipient should follow to accept a workspace invitation.</summary>
    public string BuildInvitationLink(string token)
    {
        var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/invite/{Uri.EscapeDataString(token)}";
    }
}
