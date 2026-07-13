using Microsoft.Extensions.Options;
using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

public sealed class ApplicationLinkBuilder(
    IOptions<ApplicationUrlOptions> options)
    : IApplicationLinkBuilder
{
    public string BuildInvitationLink(string token)
    {
        var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/invite/{Uri.EscapeDataString(token)}";
    }
}
