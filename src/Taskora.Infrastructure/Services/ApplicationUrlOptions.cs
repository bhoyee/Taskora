namespace TodoApp.Infrastructure.Services;

/// <summary>Configuration for building public-facing application URLs (see <see cref="ApplicationLinkBuilder"/>).</summary>
public sealed class ApplicationUrlOptions
{
    /// <summary>The externally reachable base URL of the application, without a trailing slash.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5173";
}
